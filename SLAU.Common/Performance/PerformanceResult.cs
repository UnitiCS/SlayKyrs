using SLAU.Common.Models;
using System.Text;

namespace SLAU.Common.Performance;
public class PerformanceResult
{
    public int MatrixSize { get; set; }
    public long DistributedTime { get; set; }
    public long LinearTime { get; set; }
    public bool SolutionsMatch { get; set; }
    public double MaxError { get; set; }
    public double Speedup { get; set; }
    public Matrix? LinearSolution { get; set; }
    public Matrix? DistributedSolution { get; set; }
    public Dictionary<int, double> ErrorDistribution { get; set; } = new();

    public void UpdateDistributedResults(long distributedTime, Matrix distributedSolution)
    {
        DistributedTime = distributedTime;
        DistributedSolution = distributedSolution;

        if (LinearSolution == null || distributedSolution == null)
        {
            Console.WriteLine("Warning: Cannot compare solutions - one or both solutions are null");
            return;
        }

        try
        {
            Console.WriteLine("\nAnalyzing solutions...");

            if (LinearSolution.Size != distributedSolution.Size)
            {
                throw new ArgumentException($"Solution size mismatch: Linear({LinearSolution.Size}) vs Distributed({distributedSolution.Size})");
            }

            double maxError = 0;
            int maxErrorIndex = -1;
            int matchCount = 0;
            const double tolerance = 1e-6;

            for (int i = 0; i < LinearSolution.Size; i++)
            {
                double linearValue = LinearSolution.GetConstant(i);
                double distributedValue = distributedSolution.GetConstant(i);
                double error = Math.Abs(linearValue - distributedValue);

                ErrorDistribution[i] = error;

                if (error <= tolerance)
                {
                    matchCount++;
                }

                if (error > maxError)
                {
                    maxError = error;
                    maxErrorIndex = i;
                }
            }

            MaxError = maxError;
            SolutionsMatch = maxError <= tolerance;

            if (DistributedTime > 0 && LinearTime > 0)
            {
                Speedup = LinearTime / (double)DistributedTime;
            }
            else
            {
                Speedup = 0;
                Console.WriteLine("Warning: Invalid timing values detected");
            }

            Console.WriteLine("\nPerformance Analysis:");
            Console.WriteLine($"Matrix Size: {MatrixSize}x{MatrixSize}");
            Console.WriteLine($"Linear Method: {LinearTime}ms");
            Console.WriteLine($"Distributed Method: {DistributedTime}ms");

            if (Speedup > 0)
            {
                if (Speedup > 1)
                {
                    Console.WriteLine($"Speedup achieved: {Speedup:F2}x");
                    Console.WriteLine($"Performance improvement: {((Speedup - 1) * 100):F1}%");
                    Console.WriteLine($"Time saved: {LinearTime - DistributedTime}ms");
                }
                else
                {
                    Console.WriteLine($"Performance decrease: {(1 / Speedup):F2}x slower");
                    Console.WriteLine($"Additional overhead: {DistributedTime - LinearTime}ms");
                }
            }

            Console.WriteLine("\nSolution Analysis:");
            Console.WriteLine($"Matching elements: {matchCount}/{LinearSolution.Size} ({(matchCount * 100.0 / LinearSolution.Size):F1}%)");
            Console.WriteLine($"Maximum error: {MaxError:E6}");

            if (maxErrorIndex >= 0)
            {
                Console.WriteLine("\nMaximum Error Details:");
                Console.WriteLine($"At index: {maxErrorIndex}");
                Console.WriteLine($"Linear value: {LinearSolution.GetConstant(maxErrorIndex):E10}");
                Console.WriteLine($"Distributed value: {distributedSolution.GetConstant(maxErrorIndex):E10}");
            }

            var errorRanges = ErrorDistribution.Values
                .GroupBy(e => Math.Floor(Math.Log10(Math.Max(e, 1e-15))))
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());

            Console.WriteLine("\nError Distribution:");
            foreach (var range in errorRanges)
            {
                Console.WriteLine($"10^{range.Key}: {range.Value} elements");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing solutions: {ex.Message}");
            SolutionsMatch = false;
            MaxError = double.PositiveInfinity;
            Speedup = 0;
        }
    }

    public string GetDetailedReport()
    {
        var report = new StringBuilder();
        report.AppendLine("\nPerformance Analysis Report");
        report.AppendLine("========================");
        report.AppendLine($"Matrix Size: {MatrixSize}x{MatrixSize}");
        report.AppendLine($"Linear Method Time: {LinearTime}ms");
        report.AppendLine($"Distributed Method Time: {DistributedTime}ms");

        if (Speedup > 0)
        {
            report.AppendLine($"Speedup Factor: {Speedup:F2}x");
            if (Speedup > 1)
            {
                report.AppendLine($"Performance improvement: {((Speedup - 1) * 100):F1}%");
                report.AppendLine($"Time saved: {LinearTime - DistributedTime}ms");
            }
            else
            {
                report.AppendLine($"Performance decrease: {((1 - Speedup) * 100):F1}%");
                report.AppendLine($"Additional overhead: {DistributedTime - LinearTime}ms");
            }
        }

        report.AppendLine("\nSolution Accuracy:");
        report.AppendLine($"Solutions Match: {SolutionsMatch}");
        report.AppendLine($"Maximum Error: {MaxError:E6}");

        if (ErrorDistribution.Any())
        {
            report.AppendLine("\nError Distribution Summary:");
            var percentiles = new[] { 50, 90, 95, 99 };
            var sortedErrors = ErrorDistribution.Values.OrderBy(x => x).ToList();

            foreach (var p in percentiles)
            {
                int index = (int)(p / 100.0 * (sortedErrors.Count - 1));
                report.AppendLine($"{p}th percentile: {sortedErrors[index]:E6}");
            }
        }

        return report.ToString();
    }

    public override string ToString()
    {
        return $"Performance Results:" +
               $"\nMatrix Size: {MatrixSize}x{MatrixSize}" +
               $"\nDistributed Time: {DistributedTime}ms" +
               $"\nLinear Time: {LinearTime}ms" +
               $"\nSpeedup: {Speedup:F2}x" +
               $"\nSolutions Match: {SolutionsMatch}" +
               $"\nMax Error: {MaxError:E2}";
    }
}