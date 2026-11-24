using System.Collections.Concurrent;

namespace Krackend.Demo;

public sealed record DemoCommandObservation(string RequestId, string Operation, string Phase, DateTime ObservedOnUtc);

// Test barriers only: no work is queued here and no SQL transaction is retained.
public sealed class DemoCommandObservations(bool enabled)
{
    private const int MaxObservations = 4096;
    private readonly ConcurrentDictionary<(string Request, string Operation), DemoCommandObservation> _observations = new();
    private readonly ConcurrentQueue<(string Request, string Operation)> _insertionOrder = new();
    private int _count;
    private int _trimming;

    public void Record(string requestId, string operation, string phase)
    {
        if (!enabled) return;
        var key = (requestId, operation);
        var observation = new DemoCommandObservation(requestId, operation, phase, DateTime.UtcNow);
        while (true)
        {
            if (_observations.TryAdd(key, observation))
            {
                _insertionOrder.Enqueue(key);
                Interlocked.Increment(ref _count);
                break;
            }

            if (_observations.TryGetValue(key, out var current) &&
                _observations.TryUpdate(key, observation, current))
                return;
        }

        while (Volatile.Read(ref _count) > MaxObservations)
        {
            if (Interlocked.CompareExchange(ref _trimming, 1, 0) != 0)
                return;

            try
            {
                while (Volatile.Read(ref _count) > MaxObservations &&
                       _insertionOrder.TryDequeue(out var oldest))
                {
                    if (_observations.TryRemove(oldest, out _))
                        Interlocked.Decrement(ref _count);
                }
            }
            finally
            {
                Volatile.Write(ref _trimming, 0);
            }
        }
    }

    public DemoCommandObservation[] Read(string requestId)
    {
        var matches = new List<DemoCommandObservation>();
        foreach (var observation in _observations)
        {
            if (observation.Value.RequestId == requestId)
                matches.Add(observation.Value);
        }
        return matches.ToArray();
    }
}
