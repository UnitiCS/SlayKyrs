using System.Net.Sockets;
using SLAU.Common.Network;
using SLAU.Common.Enums;
using SLAU.Common.Logging;

namespace SLAU.Server;

public class Server
{
    private readonly ILogger logger;
    private readonly int serverPort;
    private readonly int nodeStartPort;
    private readonly int expectedNodes;
    private Socket serverSocket;
    private readonly List<Socket> nodeListenerSockets;
    private readonly List<ComputeNodeInfo> computeNodes;
    private bool isRunning;
    private readonly object lockObject = new object();
    private TaskCompletionSource<bool> nodesConnectedTcs;

    // В классе Server
    private readonly NodeManager nodeManager;
    private readonly DistributedMatrixSolver distributedSolver;
    private readonly LinearMatrixSolver linearSolver;
    private readonly ClientHandler clientHandler;

    public Server(int serverPort, int nodeStartPort, int expectedNodes, ILogger? logger = null)
    {
        if (expectedNodes <= 0)
            throw new ArgumentException("Expected nodes must be positive", nameof(expectedNodes));

        if (nodeStartPort + expectedNodes > 65535)
            throw new ArgumentException("Too many nodes for available ports", nameof(expectedNodes));

        this.serverPort = serverPort;
        this.nodeStartPort = nodeStartPort;
        this.expectedNodes = expectedNodes;
        this.logger = logger ?? new ConsoleLogger();
        this.computeNodes = new List<ComputeNodeInfo>();
        this.nodeListenerSockets = new List<Socket>();
        this.nodesConnectedTcs = new TaskCompletionSource<bool>();

        this.nodeManager = new NodeManager(computeNodes, nodeListenerSockets, expectedNodes, nodeStartPort, nodesConnectedTcs);
        this.distributedSolver = new DistributedMatrixSolver(computeNodes);
        this.linearSolver = new LinearMatrixSolver();
        this.clientHandler = new ClientHandler(distributedSolver, linearSolver);
    }

    public async Task StartAsync()
    {
        try
        {
            serverSocket = TcpConnection.CreateListenerSocket(serverPort);
            isRunning = true;
            Console.WriteLine($"Server started on port {serverPort}");

            await nodeManager.InitializeListenersAsync();
            Console.WriteLine($"Waiting for {expectedNodes} compute nodes...");

            _ = nodeManager.AcceptNodesAsync();
            await nodesConnectedTcs.Task;
            Console.WriteLine("All compute nodes connected. Waiting for client...");

            while (isRunning)
            {
                var clientSocket = await serverSocket.AcceptAsync();
                _ = clientHandler.HandleClientAsync(clientSocket);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync()
    {
        lock (lockObject)
        {
            if (!isRunning) return;
            isRunning = false;
        }

        try
        {
            foreach (var node in computeNodes.Where(n => n.Socket != null))
            {
                try
                {
                    Console.WriteLine($"Sending shutdown command to node on port {node.Port}");
                    byte[] shutdownCommand = BitConverter.GetBytes((int)CommandType.Shutdown);
                    await TcpConnection.SendDataAsync(node.Socket, shutdownCommand);
                    node.Socket.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error shutting down node on port {node.Port}: {ex.Message}");
                }
            }

            foreach (var listener in nodeListenerSockets)
            {
                try
                {
                    listener.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing listener: {ex.Message}");
                }
            }

            serverSocket?.Close();
            Console.WriteLine("Server stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping server: {ex.Message}");
            throw;
        }
    }
}