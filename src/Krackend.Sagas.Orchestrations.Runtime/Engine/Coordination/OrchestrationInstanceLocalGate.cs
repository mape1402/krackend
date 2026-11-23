using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>
/// Serializes contenders for the same instance inside one runtime process before
/// they touch the distributed SQL lease. Different instances never share a gate.
/// </summary>
internal sealed class OrchestrationInstanceLocalGate
{
    private readonly ConcurrentDictionary<Id, Entry> _entries = new();

    public async ValueTask<IDisposable?> EnterAsync(
        Id instanceId,
        bool wait,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = _entries.GetOrAdd(instanceId, static _ => new Entry());
            Interlocked.Increment(ref entry.References);
            if (!_entries.TryGetValue(instanceId, out var current) || !ReferenceEquals(entry, current))
            {
                ReleaseReference(instanceId, entry);
                continue;
            }

            var entered = wait
                ? await entry.Semaphore.WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken)
                : entry.Semaphore.Wait(0);
            if (entered)
                return new Releaser(this, instanceId, entry);

            ReleaseReference(instanceId, entry);
            return null;
        }
    }

    private void Exit(Id instanceId, Entry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(instanceId, entry);
    }

    private void ReleaseReference(Id instanceId, Entry entry)
    {
        if (Interlocked.Decrement(ref entry.References) != 0)
            return;

        ((ICollection<KeyValuePair<Id, Entry>>)_entries)
            .Remove(new KeyValuePair<Id, Entry>(instanceId, entry));
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References;
    }

    private sealed class Releaser(
        OrchestrationInstanceLocalGate owner,
        Id instanceId,
        Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Exit(instanceId, entry);
        }
    }
}
