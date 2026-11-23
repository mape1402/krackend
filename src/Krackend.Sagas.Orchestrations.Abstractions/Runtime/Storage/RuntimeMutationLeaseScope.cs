namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Async-flow lease ownership used to fence persistent saga mutations.</summary>
public sealed class RuntimeMutationLeaseScope
{
    private readonly AsyncLocal<LeaseHolder> _current = new();

    /// <summary>Gets the lease owned by the current mutation.</summary>
    public OrchestrationInstanceLease Current => _current.Value?.Lease;

    /// <summary>Enters a mutation and restores its previous ownership on disposal.</summary>
    public IDisposable Enter(OrchestrationInstanceLease lease)
    {
        var previous = _current.Value;
        _current.Value = new LeaseHolder(lease);
        return new Scope(() => _current.Value = previous);
    }

    private sealed record LeaseHolder(OrchestrationInstanceLease Lease);

    private sealed class Scope(Action restore) : IDisposable
    {
        private Action _restore = restore;
        public void Dispose() => Interlocked.Exchange(ref _restore, null)?.Invoke();
    }
}
