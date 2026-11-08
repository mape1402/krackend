using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists orchestration instance state.
/// </summary>
public interface IOrchestrationInstanceRepository
{
    Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default);

    Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default);

    Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default);

    Task<OrchestrationInstance> TryGetByStartIdempotencyKey(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<OrchestrationInstanceLease> TryAcquireLease(
        Id instanceId,
        string leaseId,
        DateTime nowUtc,
        DateTime expiresOnUtc,
        CancellationToken cancellationToken = default);

    Task ReleaseLease(
        Id instanceId,
        string leaseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets non-terminal orchestration instances that can be reconciled after startup or failover.
    /// </summary>
    /// <param name="take">Maximum instances to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recoverable orchestration instances ordered by oldest update first.</returns>
    Task<IReadOnlyCollection<OrchestrationInstance>> GetRecoverable(
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<RuntimeInstanceSummary> GetSummary(
        DateTime recentSinceUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RuntimeInstanceSummary(
    int Active,
    int Waiting,
    int CompletedRecent,
    int FailedRecent,
    DateTime RecentSinceUtc);

/// <summary>
/// Provides indexed access to idle orchestration instances whose reconciliation window has elapsed.
/// </summary>
public interface IOrchestrationIdleInstanceRepository
{
    Task<IReadOnlyCollection<OrchestrationInstance>> GetIdleExpiringOnOrBefore(
        DateTime utcNow,
        int take = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>Provides paged candidates for per-artifact instance lifetime enforcement.</summary>
public interface IOrchestrationLifetimeCandidateRepository
{
    /// <summary>Returns running instances for an artifact started on or before the cutoff.</summary>
    Task<IReadOnlyCollection<Id>> GetLifetimeCandidates(
        Id artifactId, DateTime startedOnOrBeforeUtc, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>Reads lifetime candidates across artifacts in a single paged scan.</summary>
public interface IOrchestrationLifetimeScanRepository
{
    /// <summary>Returns running instances started on or before the earliest policy cutoff.</summary>
    Task<IReadOnlyCollection<OrchestrationLifetimeCandidate>> GetLifetimeCandidatePage(
        DateTime startedOnOrBeforeUtc, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>Identifiers needed to match a running instance to its deployed lifetime policy.</summary>
public sealed record OrchestrationLifetimeCandidate(Id InstanceId, Id ArtifactId, DateTime StartedOnUtc);

/// <summary>Reads projected expiration deadlines and legacy instances requiring one-time projection.</summary>
public interface IOrchestrationExpirationRepository
{
    /// <summary>Reads only running instances whose projected expiration deadline has elapsed.</summary>
    Task<IReadOnlyCollection<Id>> GetExpiredInstances(DateTime utcNow, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Reads recoverable legacy instances that do not yet have an effective lifetime policy.</summary>
    Task<IReadOnlyCollection<Id>> GetUnprojectedInstances(int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>Provides a bounded existence check for active instances of an artifact.</summary>
public interface IOrchestrationArtifactInstanceReader
{
    /// <summary>Returns whether the artifact has non-terminal instances or an unexpired instance mutation lease.</summary>
    Task<bool> HasActiveInstances(Id artifactId, CancellationToken cancellationToken = default);
}

/// <summary>Reads active or currently leased instance owners for a bounded artifact batch.</summary>
public interface IOrchestrationArtifactActivityBatchReader
{
    /// <summary>Returns distinct artifact ids with non-terminal instances or an unexpired mutation lease.</summary>
    Task<IReadOnlyCollection<Id>> GetActiveArtifactIds(IReadOnlyCollection<Id> artifactIds, CancellationToken cancellationToken = default);
}
