namespace SLAU.Common.Logging;
public class TestLogger : ILogger
{
    public void Log(string message)
    {
        // В тестах можно игнорировать логи
    }

    public void Clear()
    {
        // В тестах ничего не делаем
    }
}