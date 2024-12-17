using System;
using System.Threading.Tasks;

namespace SLAU.ComputeNode;
class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || !int.TryParse(args[0], out int port))
            {
                Console.WriteLine("Port not specified or invalid");
                return;
            }

            Console.Title = $"Compute Node (Port: {port})";
            var node = new ComputeNode(port);
            Console.WriteLine($"Starting compute node on port {port}...");

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                node.Stop();
            };

            await node.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}