using System.Net.Sockets;
using SLAU.Common.Network;
using SLAU.Common.Logging;
using SLAU.Common;

namespace SLAU.Server;
internal class NodeManager
{
    private readonly List<ComputeNodeInfo> computeNodes;
    private readonly List<Socket> nodeListenerSockets;
    private readonly int expectedNodes;
    private readonly int nodeStartPort;
    private readonly TaskCompletionSource<bool> nodesConnectedTcs;
    private readonly ILogger logger;

    public NodeManager(List<ComputeNodeInfo> computeNodes,
                      List<Socket> nodeListenerSockets,
                      int expectedNodes,
                      int nodeStartPort,
                      TaskCompletionSource<bool> nodesConnectedTcs,
                      ILogger? logger = null)
    {
        this.computeNodes = computeNodes;
        this.nodeListenerSockets = nodeListenerSockets;
        this.expectedNodes = expectedNodes;
        this.nodeStartPort = nodeStartPort;
        this.nodesConnectedTcs = nodesConnectedTcs;
        this.logger = logger ?? new ConsoleLogger();
    }

    public async Task InitializeListenersAsync()
    {
        int bufferSize = AdaptiveBuffer.CalculateBufferSize(expectedNodes);
        logger.Log($"Initializing listeners with adaptive buffer size: {bufferSize}");

        for (int i = 0; i < expectedNodes; i++)
        {
            int nodePort = nodeStartPort + i; // Уникальный порт для каждого узла
            try
            {
                var nodeListener = TcpConnection.CreateListenerSocket(nodePort);
                nodeListener.ReceiveBufferSize = bufferSize;
                nodeListener.SendBufferSize = bufferSize;
                nodeListenerSockets.Add(nodeListener);
                logger.Log($"Listening for compute node on port {nodePort} with buffer size {bufferSize}");
            }
            catch (Exception ex)
            {
                logger.Log($"Failed to initialize listener on port {nodePort}: {ex.Message}");
                throw;
            }
        }
    }

    public async Task AcceptNodesAsync()
    {
        try
        {
            var acceptTasks = new List<Task<Socket>>();

            // Создаем начальные задачи для приема подключений
            for (int i = 0; i < expectedNodes; i++)
            {
                var nodeListener = nodeListenerSockets[i];
                acceptTasks.Add(nodeListener.AcceptAsync());
            }

            logger.Clear();
            logger.Log($"=== Server Status ===");
            logger.Log($"Node ports: {nodeStartPort}-{nodeStartPort + expectedNodes - 1}");
            logger.Log($"Expected nodes: {expectedNodes}");
            logger.Log("\nWaiting for compute nodes...");
            UpdateNodeStatus();

            while (computeNodes.Count < expectedNodes)
            {
                var completedTask = await Task.WhenAny(acceptTasks);
                var nodeSocket = await completedTask;
                var nodePort = ((System.Net.IPEndPoint)nodeSocket.LocalEndPoint).Port;

                var nodeInfo = new ComputeNodeInfo
                {
                    Socket = nodeSocket,
                    Port = nodePort
                };

                computeNodes.Add(nodeInfo);
                logger.Log($"\n[{DateTime.Now:HH:mm:ss}] Node {computeNodes.Count} connected on port {nodePort}!");
                UpdateNodeStatus();

                // Удаляем завершенную задачу из списка
                acceptTasks.Remove(completedTask);

                // Если все узлы подключились, выходим из цикла
                if (computeNodes.Count == expectedNodes)
                {
                    logger.Log("\nAll compute nodes connected successfully!");
                    logger.Log("Server is ready to accept client connections.\n");
                    nodesConnectedTcs.SetResult(true);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log($"\nError accepting nodes: {ex.Message}");
            nodesConnectedTcs.SetException(ex);
            throw;
        }
    }

    private void UpdateNodeStatus()
    {
        logger.Log("\nConnected nodes status:");
        logger.Log($"╔════════════════════════════════════╗");
        logger.Log($"║ Connected: {computeNodes.Count,2}/{expectedNodes,-2} nodes                 ║");

        var status = "║ [";
        int progressBarWidth = 20;
        int filledCount = (int)((float)computeNodes.Count / expectedNodes * progressBarWidth);

        for (int i = 0; i < progressBarWidth; i++)
        {
            status += (i < filledCount) ? "█" : "░";
        }
        status += "] ║";
        logger.Log(status);

        logger.Log($"╚════════════════════════════════════╝");

        if (computeNodes.Count > 0)
        {
            logger.Log("\nActive nodes:");
            int bufferSize = AdaptiveBuffer.CalculateBufferSize(expectedNodes);
            for (int i = 0; i < computeNodes.Count; i++)
            {
                logger.Log($"Node {i + 1}: Port {computeNodes[i].Port} (Buffer: {bufferSize} bytes)");
            }
        }
    }

    // Добавим метод для динамической корректировки размеров буферов
    private void AdjustBufferSizes()
    {
        int optimalBufferSize = AdaptiveBuffer.CalculateBufferSize(expectedNodes);

        foreach (var node in computeNodes)
        {
            if (node.Socket != null && node.Socket.Connected)
            {
                try
                {
                    node.Socket.ReceiveBufferSize = optimalBufferSize;
                    node.Socket.SendBufferSize = optimalBufferSize;
                }
                catch (SocketException ex)
                {
                    logger.Log($"Warning: Could not adjust buffer size for node on port {node.Port}: {ex.Message}");
                }
            }
        }
    }
}