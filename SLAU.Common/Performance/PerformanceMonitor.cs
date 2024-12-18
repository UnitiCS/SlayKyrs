using System.Diagnostics;

namespace SLAU.Common.Performance;
public class PerformanceMonitor
{
    private readonly Dictionary<string, List<long>> _measurements;
    private readonly Dictionary<string, Stopwatch> _activeStopwatches;

    public PerformanceMonitor()
    {
        _measurements = new Dictionary<string, List<long>>();
        _activeStopwatches = new Dictionary<string, Stopwatch>();
    }

    public void StartMeasurement(string operationName)
    {
        if (_activeStopwatches.ContainsKey(operationName))
            throw new InvalidOperationException($"Measurement '{operationName}' is already running");

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        _activeStopwatches[operationName] = stopwatch;
    }

    public void StopMeasurement(string operationName)
    {
        if (!_activeStopwatches.TryGetValue(operationName, out var stopwatch))
            throw new InvalidOperationException($"No active measurement found for '{operationName}'");

        stopwatch.Stop();
        if (!_measurements.ContainsKey(operationName))
            _measurements[operationName] = new List<long>();

        _measurements[operationName].Add(stopwatch.ElapsedMilliseconds);
        _activeStopwatches.Remove(operationName);
    }

    public Dictionary<string, PerformanceStats> GetStatistics()
    {
        var stats = new Dictionary<string, PerformanceStats>();

        foreach (var kvp in _measurements)
        {
            var measurements = kvp.Value;
            if (measurements.Count == 0)
                continue;

            var total = 0L;
            var min = long.MaxValue;
            var max = long.MinValue;

            foreach (var measurement in measurements)
            {
                total += measurement;
                min = Math.Min(min, measurement);
                max = Math.Max(max, measurement);
            }

            stats[kvp.Key] = new PerformanceStats
            {
                AverageMs = (double)total / measurements.Count,
                MinMs = min,
                MaxMs = max,
                TotalMs = total,
                Count = measurements.Count
            };
        }

        return stats;
    }

    public void Reset()
    {
        _measurements.Clear();
        _activeStopwatches.Clear();
    }
}

public class PerformanceStats
{
    public double AverageMs { get; set; }
    public long MinMs { get; set; }
    public long MaxMs { get; set; }
    public long TotalMs { get; set; }
    public int Count { get; set; }

    public override string ToString()
    {
        return $"Count: {Count}, Avg: {AverageMs:F2}ms, Min: {MinMs}ms, Max: {MaxMs}ms, Total: {TotalMs}ms";
    }
}