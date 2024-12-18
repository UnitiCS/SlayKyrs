using SLAU.Common.Logging;
using System.Net;
using System.Net.Sockets;

namespace SLAU.ComputeNode;
class Program
{
    static async Task Main(string[] args)
    {
        var logger = new ConsoleLogger();

        try
        {
            if (args.Length < 1)
            {
                logger.LogError("Port number is required");
                return;
            }

            if (!int.TryParse(args[0], out int port))
            {
                logger.LogError("Invalid port number");
                return;
            }

            logger.LogInfo($"Starting compute node on port {port}");
            var tcpListener = new TcpListener(IPAddress.Any, port);

            try
            {
                tcpListener.Start();
                logger.LogInfo($"Compute node is listening on port {port}");

                while (true)
                {
                    var client = await tcpListener.AcceptTcpClientAsync();
                    logger.LogInfo($"Accepted connection from {client.Client.RemoteEndPoint}");

                    _ = HandleClientAsync(client, logger).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            logger.LogError($"Error handling client: {t.Exception}");
                        }
                    });
                }
            }
            finally
            {
                tcpListener.Stop();
                logger.LogInfo("Compute node stopped");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static async Task HandleClientAsync(TcpClient client, ILogger logger)
    {
        try
        {
            using var node = new ComputeNode(client, logger);
            await node.ProcessRequestsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }
}