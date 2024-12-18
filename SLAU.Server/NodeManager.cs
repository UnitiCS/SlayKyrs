using SLAU.Common.Logging;
using SLAU.Common.Network;
using SLAU.Server.Configuration;
using System.Collections.Concurrent;

namespace SLAU.Server;
public class NodeManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, ComputeNodeInfo> _activeNodes;
    private readonly ConcurrentDictionary<int, TcpConnection> _nodeConnections;

    public int ActiveNodesCount => _activeNodes.Count;

    public NodeManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeNodes = new ConcurrentDictionary<int, ComputeNodeInfo>();
        _nodeConnections = new ConcurrentDictionary<int, TcpConnection>();
    }

    public async Task InitializeNodeAsync(string host, int port)
    {
        var node = new ComputeNodeInfo
        {
            Id = _activeNodes.Count + 1,
            Host = host,
            Port = port,
            MaxThreads = Environment.ProcessorCount,
            IsActive = true
        };

        _logger.LogInfo($"Attempting to connect to node {node.Id} at {host}:{port}...");
        var connection = await TcpConnection.ConnectAsync(host, port, _logger);

        if (_nodeConnections.TryAdd(node.Id, connection))
        {
            if (_activeNodes.TryAdd(node.Id, node))
            {
                _logger.LogInfo($"Successfully connected to node {node.Id} at {host}:{port}");
            }
            else
            {
                connection.Dispose();
                _nodeConnections.TryRemove(node.Id, out _);
                throw new InvalidOperationException($"Failed to add node {node.Id} to active nodes");
            }
        }
        else
        {
            connection.Dispose();
            throw new InvalidOperationException($"Node {node.Id} is already connected");
        }
    }

    public async Task ShutdownNodesAsync()
    {
        _logger.LogInfo("Shutting down all compute nodes...");

        foreach (var connection in _nodeConnections.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing node connection: {ex.Message}");
            }
        }

        _nodeConnections.Clear();
        _activeNodes.Clear();

        _logger.LogInfo("All compute nodes have been shut down");
    }

    private async Task ConnectToNodeAsync(ComputeNodeInfo node)
    {
        try
        {
            _logger.LogInfo($"Attempting to connect to node {node.Id} at {node.Host}:{node.Port}...");

            int retryCount = 3;
            int retryDelay = 1000; // миллисекунды

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    var connection = await TcpConnection.ConnectAsync(node.Host, node.Port, _logger);

                    if (_nodeConnections.TryAdd(node.Id, connection))
                    {
                        _activeNodes.TryAdd(node.Id, node);
                        _logger.LogInfo($"Successfully connected to node {node.Id} at {node.Host}:{node.Port}");
                        return;
                    }
                    else
                    {
                        connection.Dispose();
                        _logger.LogWarning($"Node {node.Id} is already connected");
                        return;
                    }
                }
                catch (Exception) when (i < retryCount - 1)
                {
                    await Task.Delay(retryDelay);
                    retryDelay *= 2; // Увеличиваем задержку при каждой попытке
                }
            }

            throw new Exception("Maximum retry attempts reached");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to connect to node {node.Id} at {node.Host}:{node.Port}: {ex.Message}");
            throw;
        }
    }

    public async Task<T> SendToNodeAsync<T>(int nodeId, object data)
    {
        if (!_nodeConnections.TryGetValue(nodeId, out var connection))
        {
            throw new InvalidOperationException($"Node {nodeId} is not connected");
        }

        try
        {
            await connection.SendAsync(data);
            return await connection.ReceiveAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error communicating with node {nodeId}: {ex.Message}");
            await HandleNodeFailureAsync(nodeId);
            throw;
        }
    }

    private async Task HandleNodeFailureAsync(int nodeId)
    {
        if (_nodeConnections.TryRemove(nodeId, out var connection))
        {
            connection.Dispose();
        }
        _activeNodes.TryRemove(nodeId, out _);

        if (_activeNodes.Count == 0)
        {
            _logger.LogError("All compute nodes are unavailable");
            throw new InvalidOperationException("No compute nodes available");
        }

        _logger.LogWarning($"Node {nodeId} has been removed from active nodes. Remaining nodes: {_activeNodes.Count}");
    }

    public int GetOptimalNodeCount()
    {
        return Math.Min(_activeNodes.Count, Environment.ProcessorCount);
    }

    public IList<ComputeNodeInfo> GetAvailableNodes()
    {
        return _activeNodes.Values.ToList();
    }
}