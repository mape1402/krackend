using System.Diagnostics;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>
/// Tracks latency-sensitive ingress work so bulk timeout recovery can yield without
/// limiting the parallelism of unrelated, realtime orchestration instances.
/// </summary>
internal sealed class RuntimeForegroundActivity
{
    private int _active;
    private long _lastActivityTimestamp;

    public ActivityLease Enter()
    {
        Interlocked.Exchange(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
        Interlocked.Increment(ref _active);
        return new ActivityLease(this);
    }

    public bool ShouldYieldRecovery(TimeSpan quietPeriod)
    {
        if (Volatile.Read(ref _active) > 0)
            return true;

        var lastActivity = Interlocked.Read(ref _lastActivityTimestamp);
        return lastActivity != 0 && Stopwatch.GetElapsedTime(lastActivity) < quietPeriod;
    }

    private void Exit()
    {
        Interlocked.Exchange(ref _lastActivityTimestamp, Stopwatch.GetTimestamp());
        Interlocked.Decrement(ref _active);
    }

    internal readonly struct ActivityLease(RuntimeForegroundActivity owner) : IDisposable
    {
        public void Dispose() => owner.Exit();
    }
}
