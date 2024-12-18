using SLAU.Common.Logging;
using SLAU.Common.Models;
using SLAU.Common.Models.Commands;
using SLAU.Common.Network;
using SLAU.Common.Performance;
using System.Text;

namespace SLAU.Client;

[Serializable]
public class SolverResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public double[] Solution { get; set; }
    public Dictionary<string, PerformanceStats> PerformanceStats { get; set; }
}

public class Client : IDisposable
{
    private readonly string _serverHost;
    private readonly int _serverPort;
    private readonly ILogger _logger;
    private TcpConnection _connection;
    private bool _isDisposed;

    public Client(string serverHost, int serverPort, ILogger logger)
    {
        _serverHost = serverHost ?? throw new ArgumentNullException(nameof(serverHost));
        _serverPort = serverPort;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeNodesAsync(int nodeCount)
    {
        try
        {
            _logger.LogInfo($"Initializing {nodeCount} nodes...");
            await EnsureConnectedAsync();

            var command = new InitNodesCommand { NodeCount = nodeCount };
            _logger.LogInfo("Sending InitNodesCommand to server");
            await _connection.SendAsync(command);

            _logger.LogInfo("Waiting for server response");
            var response = await _connection.ReceiveAsync<InitNodesResult>();

            if (!response.IsSuccess)
            {
                throw new Exception(response.ErrorMessage);
            }

            _logger.LogInfo($"Successfully initialized {response.ActiveNodeCount} compute nodes");

            // Отправляем подтверждение получения результата
            await _connection.SendAsync(new CommandComplete());
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error initializing nodes: {ex.Message}");
            throw;
        }
    }

    public async Task<(double[] Solution, string Stats)> SolveAsync(Matrix matrix)
    {
        try
        {
            await EnsureConnectedAsync();

            // Создаем команду с матрицей
            var matrixCommand = new MatrixCommand(matrix);
            _logger.LogInfo("Sending matrix to server");
            await _connection.SendAsync(matrixCommand);

            // Получение результата
            _logger.LogInfo("Waiting for solution");
            var result = await _connection.ReceiveAsync<SolverResult>();

            if (!result.IsSuccess)
            {
                throw new Exception(result.ErrorMessage);
            }

            _logger.LogInfo("Solution received successfully");

            // Отправляем подтверждение получения результата
            await _connection.SendAsync(new CommandComplete());

            return (result.Solution, FormatStats(result.PerformanceStats));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error solving matrix: {ex.Message}");
            throw;
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection?.IsConnected != true)
        {
            try
            {
                _connection?.Dispose();
                _connection = await TcpConnection.ConnectAsync(_serverHost, _serverPort, _logger);
                _logger.LogInfo($"Connected to server at {_serverHost}:{_serverPort}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to server: {ex.Message}");
            }
        }
    }

    private string FormatStats(Dictionary<string, PerformanceStats> stats)
    {
        if (stats == null)
            return "Нет данных о производительности";

        var result = new StringBuilder("Статистика выполнения:\n");
        foreach (var stat in stats)
        {
            result.AppendLine($"{stat.Key}:");
            result.AppendLine($"  Среднее время: {stat.Value.AverageMs:F2} мс");
            result.AppendLine($"  Мин. время: {stat.Value.MinMs} мс");
            result.AppendLine($"  Макс. время: {stat.Value.MaxMs} мс");
            result.AppendLine($"  Всего операций: {stat.Value.Count}");
        }
        return result.ToString();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error disposing client: {ex.Message}");
        }
        finally
        {
            _isDisposed = true;
        }
    }
}