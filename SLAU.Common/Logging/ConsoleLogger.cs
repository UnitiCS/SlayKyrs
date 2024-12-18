namespace SLAU.Common.Logging;

public class ConsoleLogger : ILogger
{
    public void LogInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        Console.ResetColor();
    }

    public void LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        Console.ResetColor();
    }

    public void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        Console.ResetColor();
    }

    public void LogDebug(string message)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        Console.ResetColor();
    }
}