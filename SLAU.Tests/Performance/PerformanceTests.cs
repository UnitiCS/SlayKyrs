using SLAU.Common.Models;
using SLAU.Common.Performance;
using SLAU.Server;
using SLAU.Client;
using Xunit;
using System.Diagnostics;

namespace SLAU.Tests.Performance;
public class PerformanceTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public async Task LinearSolverPerformance(int size)
    {
        // Arrange
        var matrix = SLAU.Client.Client.GenerateRandomMatrix(size);
        var analyzer = new PerformanceAnalyzer();
        var linearSolver = new LinearMatrixSolver(); // Используем правильный солвер

        // Создаем фиктивный распределенный решатель
        Func<Matrix, Task<Matrix>> mockDistributedSolver = (Matrix m) =>
        {
            return Task.FromResult(m.Clone());
        };

        // Создаем линейный решатель
        Func<Matrix, Matrix> linearSolverFunc = (Matrix m) =>
        {
            return linearSolver.Solve(m); // Используем метод Solve из LinearMatrixSolver
        };

        // Act
        var sw = new Stopwatch();
        sw.Start();

        // Выполняем несколько итераций для более точного измерения
        for (int i = 0; i < 3; i++)
        {
            var result = await analyzer.CompareMethodsAsync(
                matrix,
                mockDistributedSolver,
                linearSolverFunc
            );
            Assert.NotNull(result.LinearSolution);
        }

        sw.Stop();
        var averageTime = sw.ElapsedTicks / 3.0;

        // Assert
        Assert.True(averageTime > 0, $"Average execution time: {averageTime} ticks");
        Console.WriteLine($"Size: {size}, Average time: {averageTime} ticks");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void MatrixGenerationPerformance(int size)
    {
        // Arrange
        var sw = Stopwatch.StartNew();
        var iterations = 5;
        var matrices = new List<Matrix>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            matrices.Add(SLAU.Client.Client.GenerateRandomMatrix(size));
        }
        sw.Stop();
        var averageTime = sw.ElapsedTicks / iterations;

        // Assert
        Assert.True(averageTime > 0, $"Average generation time: {averageTime} ticks");
        Assert.Equal(iterations, matrices.Count);
        Console.WriteLine($"Size: {size}, Average generation time: {averageTime} ticks");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void MatrixOperationsPerformance(int size)
    {
        // Arrange
        var matrix = SLAU.Client.Client.GenerateRandomMatrix(size);
        var sw = Stopwatch.StartNew();
        var operations = 0;

        // Act - выполняем больше операций для измеримого результата
        for (int k = 0; k < 10; k++) // Добавляем внешний цикл
        {
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    double value = matrix[i, j];
                    matrix[i, j] = value * 2 + 1;
                    operations++;
                }
            }
        }
        sw.Stop();

        // Assert
        var timePerOperation = sw.ElapsedTicks / (double)operations;
        Assert.True(timePerOperation > 0, $"Time per operation: {timePerOperation} ticks");
        Console.WriteLine($"Size: {size}, Operations: {operations}, Time per operation: {timePerOperation} ticks");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void MatrixMemoryUsage(int size)
    {
        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        var matrices = new List<Matrix>();
        for (int i = 0; i < 5; i++) // Создаем несколько матриц для более заметного эффекта
        {
            matrices.Add(SLAU.Client.Client.GenerateRandomMatrix(size));
        }

        var finalMemory = GC.GetTotalMemory(false);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        var expectedMinMemory = size * size * sizeof(double) * matrices.Count;
        Assert.True(memoryUsed > expectedMinMemory,
            $"Memory used ({memoryUsed} bytes) should be greater than expected minimum ({expectedMinMemory} bytes)");
        Console.WriteLine($"Size: {size}, Memory used: {memoryUsed:N0} bytes, Expected minimum: {expectedMinMemory:N0} bytes");
    }
}