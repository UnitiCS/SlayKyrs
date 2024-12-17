// SequentialGaussSolver.cs
using SlayKyrs;
using System;
using System.Diagnostics;

namespace SlayKyrs
{
    public class SequentialGaussSolver
    {
        public static (Matrix result, TimeSpan elapsed) Solve(Matrix matrix)
        {
            var stopwatch = Stopwatch.StartNew();
            Matrix result = matrix.Clone();
            int n = matrix.Size;

            // Прямой ход
            for (int k = 0; k < n - 1; k++)
            {
                for (int i = k + 1; i < n; i++)
                {
                    double factor = result[i, k] / result[k, k];

                    for (int j = k; j < n; j++)
                    {
                        result[i, j] -= factor * result[k, j];
                    }

                    result.SetConstant(i, result.GetConstant(i) -
                                        factor * result.GetConstant(k));
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

            stopwatch.Stop();
            return (result, stopwatch.Elapsed);
        }

        public static class PerformanceAnalyzer
        {
            public static void ComparePerformance(
                Matrix matrix,
                TimeSpan distributedTime,
                TimeSpan sequentialTime)
            {
                Console.WriteLine("\nPerformance Comparison:");
                Console.WriteLine($"Matrix size: {matrix.Size}x{matrix.Size}");
                Console.WriteLine($"Distributed solution time: {distributedTime.TotalSeconds:F3} seconds");
                Console.WriteLine($"Sequential solution time: {sequentialTime.TotalSeconds:F3} seconds");

                double speedup = sequentialTime.TotalSeconds / distributedTime.TotalSeconds;
                Console.WriteLine($"Speedup: {speedup:F2}x");

                // Эффективность распараллеливания (предполагая, что известно количество узлов)
                // double efficiency = speedup / numberOfNodes;
                // Console.WriteLine($"Efficiency: {efficiency:F2}");
            }

            public static void RunScalabilityTest(int[] matrixSizes, int repetitions = 3)
            {
                Console.WriteLine("\nScalability Test:");
                Console.WriteLine("Matrix Size\tSequential Time (s)\tDistributed Time (s)\tSpeedup");

                foreach (int size in matrixSizes)
                {
                    double seqAvgTime = 0;
                    double distAvgTime = 0;

                    for (int i = 0; i < repetitions; i++)
                    {
                        var matrix = Client.GenerateRandomMatrix(size);

                        // Последовательное решение
                        var seqResult = Solve(matrix);
                        seqAvgTime += seqResult.elapsed.TotalSeconds;

                        // Распределенное решение
                        // Здесь должен быть код для измерения времени распределенного решения
                        // distAvgTime += distributedResult.elapsed.TotalSeconds;
                    }

                    seqAvgTime /= repetitions;
                    distAvgTime /= repetitions;
                    double speedup = seqAvgTime / distAvgTime;

                    Console.WriteLine($"{size}\t\t{seqAvgTime:F3}\t\t{distAvgTime:F3}\t\t{speedup:F2}");
                }
            }

            public static void ValidateSolution(
                Matrix originalMatrix,
                Matrix sequentialSolution,
                Matrix distributedSolution,
                double tolerance = 1e-6)
            {
                bool seqValid = Client.VerifySolution(originalMatrix, sequentialSolution, tolerance);
                bool distValid = Client.VerifySolution(originalMatrix, distributedSolution, tolerance);

                Console.WriteLine("\nSolution Validation:");
                Console.WriteLine($"Sequential solution is valid: {seqValid}");
                Console.WriteLine($"Distributed solution is valid: {distValid}");

                if (seqValid && distValid)
                {
                    // Сравниваем решения между собой
                    bool solutionsMatch = true;
                    for (int i = 0; i < originalMatrix.Size; i++)
                    {
                        if (Math.Abs(sequentialSolution.GetConstant(i) -
                            distributedSolution.GetConstant(i)) > tolerance)
                        {
                            solutionsMatch = false;
                            break;
                        }
                    }
                    Console.WriteLine($"Solutions match each other: {solutionsMatch}");
                }
            }
        }
    }
}