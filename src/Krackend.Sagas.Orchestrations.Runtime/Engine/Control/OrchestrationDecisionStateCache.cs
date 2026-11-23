using System.Collections.Concurrent;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control;

/// <summary>
/// Transfers the last committed decision state between activations in a single runtime process.
/// Taking removes ownership from the cache; the activation returns it only after SaveChanges
/// succeeds. A failed activation therefore cannot expose uncommitted in-memory mutations.
/// </summary>
internal sealed class OrchestrationDecisionStateCache
{
    private const int MaximumEntries = 50_000;
    private readonly ConcurrentDictionary<Id, CacheEntry> _entries = new();
    private readonly bool _enabled;
    private long _generation;

    public OrchestrationDecisionStateCache(IOptions<OrchestrationCoordinationOptions> options)
    {
        // A different replica may mutate an instance without observing this process's cache.
        // Keep the optimization strictly on the single-replica/local-gate configuration.
        _enabled = options?.Value.DistributedLeaseEnabled == false;
    }

    public bool Enabled => _enabled;

    public bool TryTake(Id instanceId, out OrchestrationDecisionState state)
    {
        state = null;
        if (!_enabled || !_entries.TryRemove(instanceId, out var entry))
            return false;

        state = entry.State;
        return true;
    }

    public void Store(OrchestrationDecisionState state)
    {
        if (!_enabled || state is null || IsTerminal(state.Instance.Status))
            return;

        var generation = Interlocked.Increment(ref _generation);
        _entries[state.Instance.Id] = new CacheEntry(state, generation);
        if (_entries.Count > MaximumEntries)
            Trim(generation - MaximumEntries);
    }

    public void Discard(Id instanceId)
        => _entries.TryRemove(instanceId, out _);

    private void Trim(long oldestGenerationToKeep)
    {
        foreach (var candidate in _entries)
        {
            if (candidate.Value.Generation >= oldestGenerationToKeep)
                continue;
            ((ICollection<KeyValuePair<Id, CacheEntry>>)_entries).Remove(candidate);
        }
    }

    private static bool IsTerminal(OrchestrationInstanceStatus status)
        => status is OrchestrationInstanceStatus.Completed
            or OrchestrationInstanceStatus.CompletedWithErrors
            or OrchestrationInstanceStatus.Failed
            or OrchestrationInstanceStatus.Compensated
            or OrchestrationInstanceStatus.DeadLettered
            or OrchestrationInstanceStatus.Stopped
            or OrchestrationInstanceStatus.Aborted;

    private sealed record CacheEntry(OrchestrationDecisionState State, long Generation);
}

internal interface IOrchestrationDecisionStateLifecycle
{
    void CommitActivation();

    void AbortActivation();
}
