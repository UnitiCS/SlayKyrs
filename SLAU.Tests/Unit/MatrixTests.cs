using SLAU.Common.Models;
using Xunit;

namespace SLAU.Tests.Unit;
public class MatrixTests
{
    [Fact]
    public void Matrix_Constructor_ShouldCreateValidMatrix()
    {
        // Arrange & Act
        var matrix = new Matrix(3);

        // Assert
        Assert.Equal(3, matrix.Size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Matrix_Constructor_ShouldThrowOnInvalidSize(int size)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new Matrix(size));
    }

    [Fact]
    public void Matrix_IndexerShouldWorkCorrectly()
    {
        // Arrange
        var matrix = new Matrix(2);

        // Act
        matrix[0, 0] = 1.0;
        matrix[0, 1] = 2.0;
        matrix[1, 0] = 3.0;
        matrix[1, 1] = 4.0;

        // Assert
        Assert.Equal(1.0, matrix[0, 0]);
        Assert.Equal(2.0, matrix[0, 1]);
        Assert.Equal(3.0, matrix[1, 0]);
        Assert.Equal(4.0, matrix[1, 1]);
    }

    [Fact]
    public void Matrix_Constants_ShouldWorkCorrectly()
    {
        // Arrange
        var matrix = new Matrix(2);

        // Act
        matrix.SetConstant(0, 5.0);
        matrix.SetConstant(1, 6.0);

        // Assert
        Assert.Equal(5.0, matrix.GetConstant(0));
        Assert.Equal(6.0, matrix.GetConstant(1));
    }

    [Fact]
    public void Matrix_Clone_ShouldCreateDeepCopy()
    {
        // Arrange
        var original = new Matrix(2);
        original[0, 0] = 1.0;
        original[0, 1] = 2.0;
        original[1, 0] = 3.0;
        original[1, 1] = 4.0;
        original.SetConstant(0, 5.0);
        original.SetConstant(1, 6.0);

        // Act
        var clone = original.Clone();
        original[0, 0] = 10.0; // Изменяем оригинал
        original.SetConstant(0, 50.0);

        // Assert
        Assert.Equal(1.0, clone[0, 0]); // Клон не должен измениться
        Assert.Equal(5.0, clone.GetConstant(0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    public void Matrix_Indexer_ShouldThrowOnInvalidIndices(int row, int col)
    {
        // Arrange
        var matrix = new Matrix(2);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => matrix[row, col]);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[row, col] = 1.0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Matrix_Constants_ShouldThrowOnInvalidIndex(int index)
    {
        // Arrange
        var matrix = new Matrix(2);

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => matrix.GetConstant(index));
        Assert.Throws<IndexOutOfRangeException>(() => matrix.SetConstant(index, 1.0));
    }
}