#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class NoopRuntimeHotStateStore : IRuntimeHotStateStore
{
    public bool IsEnabled => false;

    public Task<long> CommitAsync(IReadOnlyCollection<RuntimeHotStateMutation> mutations, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Runtime hot state is not enabled.");

    public Task CacheAsync(RuntimeHotStateMutation mutation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<RuntimeHotStateRecord?> TryReadAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
        => Task.FromResult<RuntimeHotStateRecord?>(null);

    public Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadByInstanceAsync(string entityType, Id instanceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeHotStateRecord>>([]);

    public Task<RuntimeHotStateRecord?> TryReadByIndexAsync(string entityType, string indexName, string indexValue, CancellationToken cancellationToken = default)
        => Task.FromResult<RuntimeHotStateRecord?>(null);

    public Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadPendingInstancesAsync(int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeHotStateRecord>>([]);

    public Task<bool> TryAcquireLeaseAsync(Id instanceId, string leaseId, TimeSpan duration, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task ReleaseLeaseAsync(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<RuntimeHotProjectionLease?> TryAcquireProjectionLeaseAsync(int take, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        => Task.FromResult<RuntimeHotProjectionLease?>(null);

    public Task AcknowledgeProjectionAsync(RuntimeHotProjectionLease lease, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ReleaseProjectionLeaseAsync(RuntimeHotProjectionLease lease, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
