using System.Collections.Concurrent;

namespace Krackend.Demo;

public sealed record DemoCommandObservation(string RequestId, string Operation, string Phase, DateTime ObservedOnUtc);

// Test barriers only: no work is queued here and no SQL transaction is retained.
public sealed class DemoCommandObservations(bool enabled)
{
    private readonly ConcurrentDictionary<(string Request, string Operation), DemoCommandObservation> _observations = new();
    private long _updates;
    public void Record(string requestId, string operation, string phase)
    {
        if (!enabled) return;
        _observations[(requestId, operation)] = new(requestId, operation, phase, DateTime.UtcNow);
        if (Interlocked.Increment(ref _updates) % 128 != 0 || _observations.Count <= 4096) return;
        foreach (var old in _observations.OrderBy(x => x.Value.ObservedOnUtc).Take(256))
            _observations.TryRemove(old.Key, out _);
    }
    public DemoCommandObservation[] Read(string requestId)
        => _observations.Values.Where(x => x.RequestId == requestId).ToArray();
}
