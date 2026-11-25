namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.HotState;

using System.Diagnostics;

/// <summary>
/// Tracks write-ahead activity so the durable projector can yield to a live burst
/// without allowing the Redis journal to remain unprojected indefinitely.
/// </summary>
internal sealed class RuntimeHotStateProjectionActivity
{
    private long _firstPendingTimestamp;
    private long _lastMutationTimestamp;
    private int _activeForegroundTransactions;

    public void BeginForeground()
        => Interlocked.Increment(ref _activeForegroundTransactions);

    public void EndForeground()
        => Interlocked.Decrement(ref _activeForegroundTransactions);

    public void MarkPending()
    {
        var now = Stopwatch.GetTimestamp();
        Interlocked.CompareExchange(ref _firstPendingTimestamp, now, 0);
        Interlocked.Exchange(ref _lastMutationTimestamp, now);
    }

    public bool ShouldDefer(
        TimeSpan quietPeriod,
        TimeSpan maximumDelay,
        bool allowForegroundPreemption)
    {
        var first = Interlocked.Read(ref _firstPendingTimestamp);
        if (first == 0) return false;
        var last = Interlocked.Read(ref _lastMutationTimestamp);
        var recentlyActive = last != 0 && Stopwatch.GetElapsedTime(last) < quietPeriod;
        if (!allowForegroundPreemption &&
            (Volatile.Read(ref _activeForegroundTransactions) > 0 || recentlyActive))
            return true;
        if (Stopwatch.GetElapsedTime(first) >= maximumDelay) return false;
        return recentlyActive;
    }

    public void MarkDrained()
    {
        Interlocked.Exchange(ref _firstPendingTimestamp, 0);
        Interlocked.Exchange(ref _lastMutationTimestamp, 0);
    }
}
