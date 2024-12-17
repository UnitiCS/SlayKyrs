using SLAU.Common.Models;
using SLAU.Client;
using Xunit;

namespace SLAU.Tests.Unit;

public class ClientTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    public void GenerateRandomMatrix_ShouldCreateValidMatrix(int size)
    {
        // Act
        var matrix = SLAU.Client.Client.GenerateRandomMatrix(size);

        // Assert
        Assert.Equal(size, matrix.Size);

        // Проверка диагонального преобладания
        for (int i = 0; i < size; i++)
        {
            double diagElement = Math.Abs(matrix[i, i]);
            double sumOthers = 0;

            for (int j = 0; j < size; j++)
            {
                if (i != j)
                    sumOthers += Math.Abs(matrix[i, j]);
            }

            Assert.True(diagElement > sumOthers);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateRandomMatrix_ShouldThrowOnInvalidSize(int size)
    {
        Assert.Throws<ArgumentException>(() => SLAU.Client.Client.GenerateRandomMatrix(size));
    }

    [Fact]
    public void GenerateRandomMatrix_ShouldGenerateDifferentMatrices()
    {
        // Arrange
        int size = 10;

        // Act
        var matrix1 = SLAU.Client.Client.GenerateRandomMatrix(size);
        var matrix2 = SLAU.Client.Client.GenerateRandomMatrix(size);

        // Assert
        bool isDifferent = false;
        for (int i = 0; i < size && !isDifferent; i++)
        {
            for (int j = 0; j < size && !isDifferent; j++)
            {
                if (matrix1[i, j] != matrix2[i, j])
                {
                    isDifferent = true;
                }
            }
        }
        Assert.True(isDifferent);
    }

    [Fact]
    public void Client_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var client = new SLAU.Client.Client("localhost", 5000);

        // Assert
        Assert.NotNull(client);
    }
}