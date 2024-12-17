using System.Diagnostics;
using SLAU.Common.Models;

namespace SLAU.Common.Performance;

public class PerformanceAnalyzer
{
    private Matrix? lastMatrix;
    private Matrix? lastDistributedSolution;
    private long? lastDistributedTime;

    public async Task<PerformanceResult> CompareMethodsAsync(
        Matrix matrix,
        Func<Matrix, Task<Matrix>> distributedSolver,
        Func<Matrix, Matrix> linearSolver)
    {
        if (matrix == null)
            throw new ArgumentNullException(nameof(matrix));

        var result = new PerformanceResult
        {
            MatrixSize = matrix.Size
        };

        try
        {
            Console.WriteLine($"\n=== Starting Performance Comparison ===");
            Console.WriteLine($"Matrix size: {matrix.Size}x{matrix.Size}");

            // Линейный метод
            Console.WriteLine("\n=== Executing Linear Method ===");
            var matrixForLinear = matrix.Clone();
            var swLinear = Stopwatch.StartNew();
            result.LinearSolution = linearSolver(matrixForLinear);
            swLinear.Stop();
            result.LinearTime = swLinear.ElapsedMilliseconds;

            // Распределенный метод (с проверкой кэша)
            Matrix distributedSolution;
            if (IsCachedSolutionValid(matrix))
            {
                Console.WriteLine("\n=== Using Cached Distributed Solution ===");
                distributedSolution = lastDistributedSolution!;
                result.DistributedTime = lastDistributedTime ?? 0;
            }
            else
            {
                Console.WriteLine("\n=== Executing Distributed Method ===");
                var matrixForDistributed = matrix.Clone();
                var swDistributed = Stopwatch.StartNew();
                distributedSolution = await distributedSolver(matrixForDistributed);
                swDistributed.Stop();
                result.DistributedTime = swDistributed.ElapsedMilliseconds;

                // Обновление кэша
                UpdateCache(matrix, distributedSolution, result.DistributedTime);
            }

            result.UpdateDistributedResults(result.DistributedTime, distributedSolution);
            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error during performance comparison: {ex.Message}", ex);
        }
    }

    private bool IsCachedSolutionValid(Matrix matrix)
    {
        return lastMatrix != null &&
               lastDistributedSolution != null &&
               MatricesAreEqual(lastMatrix, matrix);
    }

    private void UpdateCache(Matrix matrix, Matrix solution, long time)
    {
        lastMatrix = matrix.Clone();
        lastDistributedSolution = solution;
        lastDistributedTime = time;
    }

    private bool MatricesAreEqual(Matrix m1, Matrix m2)
    {
        if (m1.Size != m2.Size) return false;

        const double epsilon = 1e-10;
        for (int i = 0; i < m1.Size; i++)
        {
            for (int j = 0; j < m1.Size; j++)
            {
                if (Math.Abs(m1[i, j] - m2[i, j]) > epsilon)
                    return false;
            }
            if (Math.Abs(m1.GetConstant(i) - m2.GetConstant(i)) > epsilon)
                return false;
        }
        return true;
    }
}