using SLAU.Common;

/// <summary>
/// Класс для хранения конфигурации сервера:
/// - Настройки портов
/// - Параметры подключения
/// - Конфигурация буферов
/// </summary>
namespace SLAU.Server;
internal static class ServerConfiguration
{
    public static int CalculateBufferSize(int matrixSize)
    {
        return AdaptiveBuffer.CalculateBufferSize(matrixSize);
    }

    public static int GetOptimalChunkSize(int totalRows, int nodeCount)
    {
        return Math.Max(1, totalRows / (nodeCount * 2));
    }
}