using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Krackend.Demo.RuntimeHost;

internal sealed class RuntimeMetricsProbe : IDisposable
{
    private const int MaxSamplesPerSeries = 20_000;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _samples = new(StringComparer.Ordinal);

    public RuntimeMetricsProbe()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Meter.Name, "Krackend.Runtime.Concurrency", StringComparison.Ordinal) ||
                instrument.Meter.Name.StartsWith("Npgsql", StringComparison.OrdinalIgnoreCase))
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            RecordMeasurement(instrument.Name, measurement, tags));
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            RecordMeasurement(instrument.Name, measurement, tags));
        _listener.Start();
    }

    public IReadOnlyDictionary<string, RuntimeMetricSummary> Snapshot()
    {
        _listener.RecordObservableInstruments();
        return _samples.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary(
            item => item.Key,
            item => Summarize(item.Value.ToArray()),
            StringComparer.Ordinal);
    }

    public void Reset() => _samples.Clear();

    public void Dispose() => _listener.Dispose();

    private void Record(string name, double measurement)
    {
        var queue = _samples.GetOrAdd(name, static _ => new ConcurrentQueue<double>());
        queue.Enqueue(measurement);
        while (queue.Count > MaxSamplesPerSeries)
            queue.TryDequeue(out _);
    }

    private void RecordMeasurement(
        string instrumentName,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Record(instrumentName, measurement);
        if (tags.Length > 0)
            Record(BuildSeriesName(instrumentName, tags), measurement);
    }

    private static string BuildSeriesName(
        string instrumentName,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0) return instrumentName;
        return instrumentName + "{" + string.Join(",", tags.ToArray()
            .OrderBy(tag => tag.Key, StringComparer.Ordinal)
            .Select(tag => $"{tag.Key}={tag.Value}")) + "}";
    }

    private static RuntimeMetricSummary Summarize(double[] values)
    {
        if (values.Length == 0) return new RuntimeMetricSummary(0, 0, 0, 0, 0, 0);
        Array.Sort(values);
        return new RuntimeMetricSummary(
            values.Length,
            values.Average(),
            values[0],
            Percentile(values, .50),
            Percentile(values, .95),
            values[^1]);
    }

    private static double Percentile(double[] values, double percentile)
        => values[Math.Clamp((int)Math.Ceiling(values.Length * percentile) - 1, 0, values.Length - 1)];
}

internal sealed record RuntimeMetricSummary(
    int Count,
    double Mean,
    double Min,
    double P50,
    double P95,
    double Max);
