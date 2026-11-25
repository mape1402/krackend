#nullable enable

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Provides an optional database-agnostic write-ahead hot-state layer for runtime
/// execution data. Implementations must commit a mutation batch atomically before
/// acknowledging it to the engine and retain it until the durable projector confirms it.
/// </summary>
public interface IRuntimeHotStateStore
{
    /// <summary>Gets whether hot-state routing is enabled for this host.</summary>
    bool IsEnabled { get; }

    /// <summary>Atomically commits one engine activation to hot state and its projection journal.</summary>
    Task<long> CommitAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically commits one engine activation and releases its distributed
    /// mutation lease. Providers may override this to avoid a second storage round
    /// trip while retaining the same fencing boundary.
    /// </summary>
    async Task<long> CommitAndReleaseAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        var sequence = await CommitAsync(mutations, cancellationToken);
        await ReleaseLeaseAsync(instanceId, leaseId, cancellationToken);
        return sequence;
    }

    /// <summary>Caches a durable entity without adding projection work.</summary>
    Task CacheAsync(RuntimeHotStateMutation mutation, CancellationToken cancellationToken = default);

    /// <summary>Reads the latest hot snapshot for an entity.</summary>
    Task<RuntimeHotStateRecord?> TryReadAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the latest hot snapshots of one type owned by an instance.</summary>
    Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadByInstanceAsync(
        string entityType,
        Id instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves a stable secondary lookup maintained by the hot store.</summary>
    Task<RuntimeHotStateRecord?> TryReadByIndexAsync(
        string entityType,
        string indexName,
        string indexValue,
        CancellationToken cancellationToken = default);

    /// <summary>Reads instance snapshots newer than the durable projection watermark.</summary>
    Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadPendingInstancesAsync(
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Attempts to acquire or renew the distributed mutation lease for an instance.</summary>
    Task<bool> TryAcquireLeaseAsync(
        Id instanceId,
        string leaseId,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>Releases the distributed mutation lease only when owned by the supplied token.</summary>
    Task ReleaseLeaseAsync(
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken = default);

    /// <summary>Attempts to reserve the oldest unprojected mutation batches.</summary>
    Task<RuntimeHotProjectionLease?> TryAcquireProjectionLeaseAsync(
        int take,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>Confirms that every batch in the lease was committed to durable storage.</summary>
    Task AcknowledgeProjectionAsync(
        RuntimeHotProjectionLease lease,
        CancellationToken cancellationToken = default);

    /// <summary>Releases a projection lease without removing its pending batches.</summary>
    Task ReleaseProjectionLeaseAsync(
        RuntimeHotProjectionLease lease,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes already-projected terminal materializations from the hot store in bounded batches.
    /// Active and unprojected instances must never be removed.
    /// </summary>
    Task<int> CleanupProjectedTerminalStateAsync(
        int take,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

/// <summary>
/// Reads the complete hot execution graph owned by one instance in one provider operation.
/// Providers that support this capability avoid rebuilding a callback from several independent
/// entity and owner-index reads.
/// </summary>
public interface IRuntimeHotStateInstanceGraphStore
{
    /// <summary>Reads one consistent materialized execution graph.</summary>
    Task<IReadOnlyCollection<RuntimeHotStateRecord>> ReadInstanceGraphAsync(
        Id instanceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Atomically acquires an instance mutation lease and returns the exact graph version
/// protected by that lease. Implementations may reuse a process-local graph only after
/// the durable hot store confirms that its version is still current.
/// </summary>
public interface IRuntimeHotStateLeaseGraphStore
{
    /// <summary>
    /// Returns the fenced graph snapshot, or <see langword="null"/> when another owner
    /// currently holds the instance lease.
    /// </summary>
    Task<RuntimeHotStateLeaseGraph?> TryAcquireLeaseWithGraphAsync(
        Id instanceId,
        string leaseId,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides optimistic graph fencing for hot stores whose atomic commit can reject
/// a stale graph version. This removes lease round trips from the uncontended path
/// without allowing concurrent replicas to overwrite each other.
/// </summary>
public interface IRuntimeHotStateOptimisticGraphStore
{
    /// <summary>Reads a stable graph and the version that must fence its next commit.</summary>
    Task<RuntimeHotStateLeaseGraph> ReadGraphFenceAsync(
        Id instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Commits only when the protected graph still has <paramref name="expectedVersion"/>.</summary>
    Task<long> CommitFencedAsync(
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        Id instanceId,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores one complete orchestration tracking aggregate under its instance id.
/// Initial aggregates are cached only after their durable creation commits.
/// </summary>
public interface IRuntimeHotStateAggregateStore
{
    /// <summary>Caches a durably committed orchestration aggregate without scheduling projection.</summary>
    Task CacheAggregateAsync(
        Id instanceId,
        IReadOnlyCollection<RuntimeHotStateMutation> mutations,
        CancellationToken cancellationToken = default);
}

/// <summary>One lease-protected instance graph snapshot.</summary>
public sealed record RuntimeHotStateLeaseGraph(
    long Version,
    IReadOnlyCollection<RuntimeHotStateRecord> Records,
    bool IsOptimisticFence = false);

/// <summary>
/// Stores the transport publication receipt separately from the execution graph. A callback or
/// recovery pass can fold it into the next functional state commit without forcing an otherwise
/// redundant full task mutation on the dispatch hot path.
/// </summary>
public interface IRuntimeHotStateDispatchReceiptStore
{
    /// <summary>Records the first confirmed physical publication.</summary>
    Task RecordDispatchPublicationAsync(
        Id dispatchId,
        DateTime publishedOnUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a previously recorded physical publication.</summary>
    Task<DateTime?> TryReadDispatchPublicationAsync(
        Id dispatchId,
        CancellationToken cancellationToken = default);
}

/// <summary>One database-independent entity mutation captured from an engine activation.</summary>
public sealed record RuntimeHotStateMutation(
    string EntityType,
    string EntityId,
    string? InstanceId,
    RuntimeHotStateOperation Operation,
    string Payload,
    IReadOnlyDictionary<string, string>? Indexes = null);

/// <summary>The latest hot materialization of one runtime entity.</summary>
public sealed record RuntimeHotStateRecord(
    string EntityType,
    string EntityId,
    string? InstanceId,
    RuntimeHotStateOperation Operation,
    string Payload,
    long Sequence);

/// <summary>A durable projection journal entry containing one atomic activation.</summary>
public sealed record RuntimeHotProjectionBatch(
    long Sequence,
    IReadOnlyCollection<RuntimeHotStateMutation> Mutations,
    string? InstanceId = null);

/// <summary>A fenced reservation of ordered projection batches.</summary>
public sealed record RuntimeHotProjectionLease(
    string Token,
    IReadOnlyCollection<RuntimeHotProjectionBatch> Batches);

/// <summary>Supported hot-state mutation operations.</summary>
public enum RuntimeHotStateOperation
{
    Upsert = 0,
    Delete = 1
}

/// <summary>Projects hot-state batches to the configured durable provider.</summary>
public interface IRuntimeHotStateProjectionSink
{
    /// <summary>Applies ordered mutation batches idempotently in durable storage.</summary>
    Task ProjectAsync(
        IReadOnlyCollection<RuntimeHotProjectionBatch> batches,
        CancellationToken cancellationToken = default);
}
