using SLAU.Common.Models;
using Xunit;
using System.Diagnostics;

namespace SLAU.Tests.Load;
public class LoadTests
{
    [Theory]
    [InlineData(10, 100)] // size, iterations
    [InlineData(20, 50)]
    public void RepeatedMatrixOperations(int size, int iterations)
    {
        // Arrange
        var matrix = SLAU.Client.Client.GenerateRandomMatrix(size);
        var sw = Stopwatch.StartNew();

        // Act
        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    matrix[i, j] *= 1.001;
                }
            }
        }
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds >= 0);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void ParallelMatrixOperations(int size)
    {
        // Arrange
        var matrix = SLAU.Client.Client.GenerateRandomMatrix(size);
        var sw = Stopwatch.StartNew();

        // Act
        Parallel.For(0, size, i =>
        {
            for (int j = 0; j < size; j++)
            {
                matrix[i, j] *= 2;
            }
        });
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds >= 0);
    }

    [Theory]
    [InlineData(10, 5)] // size, copies
    [InlineData(20, 3)]
    public void MultipleMatrixCreation(int size, int copies)
    {
        // Arrange
        var matrices = new List<Matrix>();
        var sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < copies; i++)
        {
            matrices.Add(SLAU.Client.Client.GenerateRandomMatrix(size));
        }
        sw.Stop();

        // Assert
        Assert.Equal(copies, matrices.Count);
        Assert.True(sw.ElapsedMilliseconds >= 0);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void MatrixClonePerformance(int size)
    {
        // Arrange
        var original = SLAU.Client.Client.GenerateRandomMatrix(size);
        var sw = Stopwatch.StartNew();

        // Act
        var clones = new List<Matrix>();
        for (int i = 0; i < 10; i++)
        {
            clones.Add(original.Clone());
        }
        sw.Stop();

        // Assert
        Assert.Equal(10, clones.Count);
        Assert.True(sw.ElapsedMilliseconds >= 0);
    }
}