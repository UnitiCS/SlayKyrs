using SLAU.Common.Logging;
using SLAU.Common.Models;
using SLAU.Common.Models.Commands;
using SLAU.Common.Models.Results.Base;
using SLAU.Common.Network;
using SLAU.Common.Performance;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SLAU.Server;
public class Server : IDisposable
{
    private readonly int _port;
    private readonly ILogger _logger;
    private readonly NodeManager _nodeManager;
    private readonly DistributedMatrixSolver _distributedSolver;
    private readonly LinearMatrixSolver _linearSolver;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly List<Process> _computeNodeProcesses;
    private readonly TcpListener _listener;
    private bool _isRunning;
    private bool _isDisposed;

    public Server(int port, ILogger logger)
    {
        _port = port;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = new PerformanceMonitor();
        _nodeManager = new NodeManager(logger);
        _distributedSolver = new DistributedMatrixSolver(_nodeManager, logger, _performanceMonitor);
        _linearSolver = new LinearMatrixSolver(logger, _performanceMonitor);
        _computeNodeProcesses = new List<Process>();
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public async Task StartAsync()
    {
        if (_isRunning)
            return;

        try
        {
            _listener.Start();
            _isRunning = true;
            _logger.LogInfo($"Server started on port {_port}");

            while (_isRunning)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Server error: {ex.Message}");
            throw;
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var connection = new TcpConnection(client, _logger);
        try
        {
            _logger.LogInfo($"Client connected: {connection.RemoteEndPoint}");

            while (connection.IsConnected && _isRunning)
            {
                try
                {
                    var message = await connection.ReceiveAsync<object>();
                    _logger.LogInfo($"Received message of type: {message?.GetType().Name ?? "null"}");

                    if (message is InitNodesCommand initCommand)
                    {
                        _logger.LogInfo($"Processing InitNodesCommand: initializing {initCommand.NodeCount} nodes...");
                        try
                        {
                            await InitializeNodesAsync(initCommand.NodeCount);
                            var response = new InitNodesResult
                            {
                                IsSuccess = true,
                                ActiveNodeCount = _nodeManager.ActiveNodesCount
                            };
                            await connection.SendAsync(response);
                            _logger.LogInfo($"Nodes initialization completed successfully");

                            // Ждем подтверждения от клиента
                            var confirmation = await connection.ReceiveAsync<CommandComplete>();
                            _logger.LogInfo("Client confirmed command completion");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to initialize nodes: {ex.Message}");
                            await connection.SendAsync(new InitNodesResult
                            {
                                IsSuccess = false,
                                ErrorMessage = ex.Message
                            });

                            // Ждем подтверждения от клиента даже в случае ошибки
                            await connection.ReceiveAsync<CommandComplete>();
                        }
                    }
                    else if (message is MatrixCommand matrixCommand)
                    {
                        _logger.LogInfo($"Processing MatrixCommand");
                        try
                        {
                            var matrix = matrixCommand.ToMatrix();
                            _logger.LogInfo($"Matrix converted successfully: {matrix.Rows}x{matrix.Columns}");

                            _performanceMonitor.StartMeasurement("distributed_solving");
                            var distributedResult = await _distributedSolver.SolveAsync(matrix);
                            _performanceMonitor.StopMeasurement("distributed_solving");
                            _logger.LogInfo("Distributed solving completed");

                            _performanceMonitor.StartMeasurement("linear_solving");
                            var linearResult = await _linearSolver.SolveAsync(matrix);
                            _performanceMonitor.StopMeasurement("linear_solving");
                            _logger.LogInfo("Linear solving completed");

                            var stats = _performanceMonitor.GetStatistics();
                            var result = new SolverResult
                            {
                                IsSuccess = true,
                                Solution = distributedResult,
                                PerformanceStats = stats
                            };

                            await connection.SendAsync(result);
                            _logger.LogInfo("Matrix solution sent to client");

                            // Ждем подтверждения от клиента
                            var confirmation = await connection.ReceiveAsync<CommandComplete>();
                            _logger.LogInfo("Client confirmed matrix solution received");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error solving matrix: {ex.Message}");
                            await connection.SendAsync(new SolverResult
                            {
                                IsSuccess = false,
                                ErrorMessage = ex.Message
                            });

                            // Ждем подтверждения от клиента даже в случае ошибки
                            await connection.ReceiveAsync<CommandComplete>();
                        }
                    }
                    else if (message is CommandComplete)
                    {
                        _logger.LogInfo("Received command completion confirmation");
                        continue;
                    }
                    else
                    {
                        _logger.LogWarning($"Unknown message type: {message?.GetType().Name ?? "null"}");
                        await connection.SendAsync(new BaseResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Unknown message type: {message?.GetType().Name ?? "null"}"
                        });

                        // Ждем подтверждения от клиента
                        await connection.ReceiveAsync<CommandComplete>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing message: {ex.Message}");
                    try
                    {
                        await connection.SendAsync(new BaseResult
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Server error: {ex.Message}"
                        });

                        // Ждем подтверждения от клиента
                        await connection.ReceiveAsync<CommandComplete>();
                    }
                    catch (Exception sendEx)
                    {
                        _logger.LogError($"Failed to send error response: {sendEx.Message}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Fatal error handling client {connection.RemoteEndPoint}: {ex.Message}");
        }
        finally
        {
            _logger.LogInfo($"Client disconnected: {connection.RemoteEndPoint}");
            try
            {
                await _nodeManager.ShutdownNodesAsync();
                _logger.LogInfo("All compute nodes shut down");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error shutting down compute nodes: {ex.Message}");
            }
        }
    }

    private async Task InitializeNodesAsync(int nodeCount)
    {
        try
        {
            // Останавливаем все существующие узлы
            await StopComputeNodesAsync();

            _logger.LogInfo($"Starting {nodeCount} compute nodes...");
            var basePort = 5001;
            var startedNodes = new List<(int Port, Process Process)>();

            // Запускаем процессы узлов
            for (int i = 0; i < nodeCount; i++)
            {
                try
                {
                    var port = basePort + i;
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"run --project \"{GetComputeNodeProjectPath()}\" -- {port}",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Normal
                    };

                    var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        startedNodes.Add((port, process));
                        _computeNodeProcesses.Add(process);
                        _logger.LogInfo($"Started compute node process for port {port}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to start compute node process for port {basePort + i}: {ex.Message}");
                }
            }

            // Ждем запуска процессов
            _logger.LogInfo("Waiting for compute nodes to initialize...");
            await Task.Delay(5000);

            // Очищаем все существующие подключения в NodeManager
            await _nodeManager.ShutdownNodesAsync();

            // Подключаемся к узлам
            var connectionTasks = new List<Task>();
            foreach (var nodeInfo in startedNodes)
            {
                connectionTasks.Add(Task.Run(async () =>
                {
                    bool connected = false;
                    int retryCount = 3;
                    int retryDelay = 2000;

                    for (int attempt = 1; attempt <= retryCount && !connected; attempt++)
                    {
                        try
                        {
                            _logger.LogInfo($"Attempting to connect to node on port {nodeInfo.Port} (attempt {attempt}/{retryCount})");
                            await _nodeManager.InitializeNodeAsync("localhost", nodeInfo.Port);
                            connected = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Attempt {attempt} failed to connect to node on port {nodeInfo.Port}: {ex.Message}");
                            if (attempt < retryCount)
                            {
                                await Task.Delay(retryDelay);
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(connectionTasks);

            int activeNodes = _nodeManager.ActiveNodesCount;
            if (activeNodes == 0)
            {
                throw new InvalidOperationException("Failed to initialize any compute nodes");
            }

            _logger.LogInfo($"Successfully initialized {activeNodes} compute nodes");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during nodes initialization: {ex.Message}");
            throw;
        }
    }

    private string GetComputeNodeProjectPath()
    {
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string solutionDirectory = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", ".."));
        return Path.Combine(solutionDirectory, "SLAU.ComputeNode");
    }

    private async Task StopComputeNodesAsync()
    {
        foreach (var process in _computeNodeProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping compute node: {ex.Message}");
            }
        }
        _computeNodeProcesses.Clear();
        await _nodeManager.ShutdownNodesAsync();
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        try
        {
            _isRunning = false;
            _listener.Stop();
            await StopComputeNodesAsync();
            _logger.LogInfo("Server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error stopping server: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isRunning = false;
        _listener.Stop();

        foreach (var process in _computeNodeProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing compute node process: {ex.Message}");
            }
        }
        _computeNodeProcesses.Clear();

        _isDisposed = true;
    }
}

[Serializable]
public class SolverResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public double[] Solution { get; set; }
    public Dictionary<string, PerformanceStats> PerformanceStats { get; set; }
}