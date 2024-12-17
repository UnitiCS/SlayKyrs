namespace SLAU.Common.Logging;
public interface ILogger
{
    void Log(string message);
    void Clear();
}