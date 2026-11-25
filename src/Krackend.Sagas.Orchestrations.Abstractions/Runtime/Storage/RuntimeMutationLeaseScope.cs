namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Async-flow lease ownership used to fence persistent saga mutations.</summary>
public sealed class RuntimeMutationLeaseScope
{
    private readonly AsyncLocal<LeaseHolder> _current = new();

    /// <summary>Gets the lease owned by the current mutation.</summary>
    public OrchestrationInstanceLease Current => _current.Value?.Lease;

    /// <summary>Gets whether the current lease was released by its atomic commit.</summary>
    public bool ReleasedByCommit => _current.Value?.ReleasedByCommit == true;

    /// <summary>
    /// Gets the graph version atomically observed while acquiring the current lease.
    /// </summary>
    public RuntimeHotStateLeaseGraph CurrentGraph => _current.Value?.Graph;

    /// <summary>Marks the current lease as released by the storage commit.</summary>
    public void MarkReleasedByCommit(string leaseId)
    {
        var holder = _current.Value;
        if (holder is not null && string.Equals(holder.Lease.LeaseId, leaseId, StringComparison.Ordinal))
            holder.ReleasedByCommit = true;
    }

    /// <summary>Enters a mutation and restores its previous ownership on disposal.</summary>
    public IDisposable Enter(OrchestrationInstanceLease lease)
        => Enter(lease, null);

    /// <summary>Enters a mutation with its lease-protected hot graph snapshot.</summary>
    public IDisposable Enter(
        OrchestrationInstanceLease lease,
        RuntimeHotStateLeaseGraph graph)
    {
        var previous = _current.Value;
        _current.Value = new LeaseHolder(lease, graph);
        return new Scope(() => _current.Value = previous);
    }

    private sealed class LeaseHolder(
        OrchestrationInstanceLease lease,
        RuntimeHotStateLeaseGraph graph)
    {
        public OrchestrationInstanceLease Lease { get; } = lease;
        public RuntimeHotStateLeaseGraph Graph { get; } = graph;
        public bool ReleasedByCommit { get; set; }
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        private Action _restore = restore;
        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
