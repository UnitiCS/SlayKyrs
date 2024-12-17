namespace SLAU.Common;
public static class AdaptiveBuffer
{
    private const int MIN_BUFFER_SIZE = 100;
    private const int MAX_BUFFER_SIZE = 50000;
    private const int MEMORY_THRESHOLD_MB = 1024; // 1 GB

    public static int CalculateBufferSize(int matrixSize)
    {
        // Получаем доступную память
        var availableMemoryMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);

        // Вычисляем размер одной строки матрицы (в байтах)
        long bytesPerRow = matrixSize * sizeof(double);

        // Вычисляем оптимальный размер буфера
        int optimalSize = (int)Math.Min(
            matrixSize,
            (MEMORY_THRESHOLD_MB * 1024 * 1024) / bytesPerRow
        );

        // Ограничиваем размер буфера
        return Math.Clamp(optimalSize, MIN_BUFFER_SIZE, MAX_BUFFER_SIZE);
    }
}