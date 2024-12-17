using SLAU.Common.Models;

namespace SLAU.Server;
public class LinearMatrixSolver
{
    public Matrix Solve(Matrix matrix)
    {
        Console.WriteLine("\n=== Starting Linear Gaussian Elimination ===");
        int n = matrix.Size;
        Matrix result = matrix.Clone();

        try
        {
            // Масштабирование матрицы
            double[] rowScales = new double[n];
            for (int i = 0; i < n; i++)
            {
                double maxInRow = 0;
                for (int j = 0; j < n; j++)
                {
                    maxInRow = Math.Max(maxInRow, Math.Abs(result[i, j]));
                }
                if (maxInRow > 0)
                {
                    rowScales[i] = 1.0 / maxInRow;
                    for (int j = 0; j < n; j++)
                    {
                        result[i, j] *= rowScales[i];
                    }
                    result.SetConstant(i, result.GetConstant(i) * rowScales[i]);
                }
            }

            // Прямой ход
            for (int k = 0; k < n - 1; k++)
            {
                if (k % 1000 == 0)
                {
                    Console.WriteLine($"Linear method: step {k + 1}/{n - 1}");
                }

                int maxRow = k;
                double maxVal = Math.Abs(result[k, k]);
                for (int i = k + 1; i < n; i++)
                {
                    double absVal = Math.Abs(result[i, k]);
                    if (absVal > maxVal)
                    {
                        maxVal = absVal;
                        maxRow = i;
                    }
                }

                if (maxRow != k)
                {
                    result.SwapRows(k, maxRow);
                    double tempScale = rowScales[k];
                    rowScales[k] = rowScales[maxRow];
                    rowScales[maxRow] = tempScale;
                }

                if (Math.Abs(result[k, k]) < 1e-12)
                {
                    throw new Exception($"Matrix is numerically singular at step {k}");
                }

                for (int i = k + 1; i < n; i++)
                {
                    double factor = result[i, k] / result[k, k];
                    for (int j = k; j < n; j++)
                    {
                        result[i, j] -= factor * result[k, j];
                    }
                    result.SetConstant(i, result.GetConstant(i) - factor * result.GetConstant(k));
                }
            }

            // Обратный ход
            for (int i = n - 1; i >= 0; i--)
            {
                double sum = 0;
                for (int j = i + 1; j < n; j++)
                {
                    sum += result[i, j] * result.GetConstant(j);
                }
                result.SetConstant(i, (result.GetConstant(i) - sum) / result[i, i]);
            }

            // Восстановление масштаба
            for (int i = 0; i < n; i++)
            {
                if (rowScales[i] > 0)
                {
                    result.SetConstant(i, result.GetConstant(i) / rowScales[i]);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in linear solver: {ex.Message}");
            throw;
        }
    }
}