using System.Diagnostics;
using System.Net.Sockets;
using System.Net;

namespace SLAU.Server;
class Program
{
    private const int DEFAULT_SERVER_PORT = 5000;
    private const int DEFAULT_NODE_PORT = 5001;
    private const int DEFAULT_NODE_COUNT = 3;

    static async Task Main(string[] args)
    {
        try
        {
            Console.Title = "SLAU Server";
            Console.WriteLine("=== SLAU Solver Server ===");

            int serverPort = DEFAULT_SERVER_PORT;
            int nodePort = DEFAULT_NODE_PORT;
            int nodeCount = DEFAULT_NODE_COUNT;

            // Проверяем аргументы командной строки
            if (args.Length >= 3)
            {
                if (!int.TryParse(args[0], out serverPort) ||
                    !int.TryParse(args[1], out nodePort) ||
                    !int.TryParse(args[2], out nodeCount))
                {
                    Console.WriteLine("Invalid command line arguments. Using default values.");
                    serverPort = DEFAULT_SERVER_PORT;
                    nodePort = DEFAULT_NODE_PORT;
                    nodeCount = DEFAULT_NODE_COUNT;
                }
            }

            // Автоматический запуск вычислительных узлов
            await LaunchComputeNodes(nodeCount, nodePort);

            // Запуск сервера
            var server = new Server(serverPort, nodePort, nodeCount);

            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                await server.StopAsync();
            };

            await server.StartAsync();

            Console.WriteLine("\nPress Ctrl+C to stop the server...");
            await Task.Delay(-1); // Бесконечное ожидание
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    private static async Task LaunchComputeNodes(int count, int startPort)
    {
        try
        {
            string executablePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SLAU.ComputeNode.exe");

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("Compute node executable not found", executablePath);
            }

            Console.WriteLine($"Launching {count} compute nodes...");

            for (int i = 0; i < count; i++)
            {
                int nodePort = startPort + i;
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = nodePort.ToString(),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process.Start(startInfo);
                Console.WriteLine($"Launched compute node on port {nodePort}");
                await Task.Delay(500); // Небольшая задержка между запусками
            }

            await Task.Delay(1000); // Даем время на инициализацию
            Console.WriteLine("All compute nodes launched successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching compute nodes: {ex.Message}");
            throw;
        }
    }
}