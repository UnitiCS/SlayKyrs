using SLAU.Common.Logging;

namespace SLAU.Server;
class Program
{
    static async Task Main(string[] args)
    {
        var logger = new ConsoleLogger();
        try
        {
            logger.LogInfo("Starting SLAU Server...");
            const int serverPort = 5000;

            using var server = new Server(serverPort, logger);

            Console.CancelKeyPress += async (s, e) =>
            {
                e.Cancel = true;
                logger.LogInfo("Shutdown requested. Stopping server...");
                await server.StopAsync();
            };

            await server.StartAsync();
        }
        catch (Exception ex)
        {
            logger.LogError($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}