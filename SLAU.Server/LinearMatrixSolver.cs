using SLAU.Common.Logging;
using SLAU.Common.Models;
using SLAU.Common.Performance;

namespace SLAU.Server;
public class LinearMatrixSolver
{
    private readonly ILogger _logger;
    private readonly PerformanceMonitor _performanceMonitor;

    public LinearMatrixSolver(ILogger logger, PerformanceMonitor performanceMonitor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
    }

    public async Task<double[]> SolveAsync(Matrix matrix)
    {
        return await Task.Run(() =>
        {
            try
            {
                _performanceMonitor.StartMeasurement("linear_gauss");
                var result = new double[matrix.Rows];
                var augmentedMatrix = CreateAugmentedMatrix(matrix);

                // Прямой ход метода Гаусса
                for (int i = 0; i < matrix.Rows; i++)
                {
                    // Поиск максимального элемента в столбце
                    int maxRow = FindMaxElementRow(augmentedMatrix, i, matrix.Rows);
                    if (Math.Abs(augmentedMatrix[maxRow, i]) < 1e-10)
                    {
                        throw new InvalidOperationException("Matrix is singular");
                    }

                    // Обмен строк
                    if (maxRow != i)
                    {
                        SwapRows(augmentedMatrix, i, maxRow);
                    }

                    // Нормализация строки
                    NormalizeRow(augmentedMatrix, i, matrix.Columns);

                    // Исключение переменной из остальных уравнений
                    EliminateVariable(augmentedMatrix, i, matrix.Rows, matrix.Columns);
                }

                // Обратный ход
                for (int i = matrix.Rows - 1; i >= 0; i--)
                {
                    result[i] = augmentedMatrix[i, matrix.Columns];
                }

                _performanceMonitor.StopMeasurement("linear_gauss");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in linear solver: {ex.Message}");
                throw;
            }
        });
    }

    private double[,] CreateAugmentedMatrix(Matrix matrix)
    {
        var augmented = new double[matrix.Rows, matrix.Columns + 1];

        for (int i = 0; i < matrix.Rows; i++)
        {
            for (int j = 0; j < matrix.Columns; j++)
            {
                augmented[i, j] = matrix[i, j];
            }
            augmented[i, matrix.Columns] = matrix.GetFreeTerm(i);
        }

        return augmented;
    }

    private int FindMaxElementRow(double[,] matrix, int col, int rowCount)
    {
        int maxRow = col;
        double maxValue = Math.Abs(matrix[col, col]);

        for (int i = col + 1; i < rowCount; i++)
        {
            if (Math.Abs(matrix[i, col]) > maxValue)
            {
                maxValue = Math.Abs(matrix[i, col]);
                maxRow = i;
            }
        }

        return maxRow;
    }

    private void SwapRows(double[,] matrix, int row1, int row2)
    {
        int cols = matrix.GetLength(1);
        for (int j = 0; j < cols; j++)
        {
            double temp = matrix[row1, j];
            matrix[row1, j] = matrix[row2, j];
            matrix[row2, j] = temp;
        }
    }

    private void NormalizeRow(double[,] matrix, int row, int cols)
    {
        double pivot = matrix[row, row];
        for (int j = row; j <= cols; j++)
        {
            matrix[row, j] /= pivot;
        }
    }

    private void EliminateVariable(double[,] matrix, int row, int rowCount, int colCount)
    {
        for (int i = 0; i < rowCount; i++)
        {
            if (i != row)
            {
                double factor = matrix[i, row];
                for (int j = row; j <= colCount; j++)
                {
                    matrix[i, j] -= factor * matrix[row, j];
                }
            }
        }
    }
}