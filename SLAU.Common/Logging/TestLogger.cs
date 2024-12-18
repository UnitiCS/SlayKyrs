using System.Collections.Generic;

namespace SLAU.Common.Logging
{
    public class TestLogger : ILogger
    {
        public List<string> InfoLogs { get; } = new List<string>();
        public List<string> WarningLogs { get; } = new List<string>();
        public List<string> ErrorLogs { get; } = new List<string>();
        public List<string> DebugLogs { get; } = new List<string>();

        public void LogInfo(string message)
        {
            InfoLogs.Add(message);
        }

        public void LogWarning(string message)
        {
            WarningLogs.Add(message);
        }

        public void LogError(string message)
        {
            ErrorLogs.Add(message);
        }

        public void LogDebug(string message)
        {
            DebugLogs.Add(message);
        }

        public void Clear()
        {
            InfoLogs.Clear();
            WarningLogs.Clear();
            ErrorLogs.Clear();
            DebugLogs.Clear();
        }
    }
}