using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>Computes one deterministic statistics projection step.</summary>
public interface ISagaStatisticsProjector
{
    /// <summary>Applies a consecutive journal entry or reports a duplicate or missing predecessor.</summary>
    SagaStatisticsProjectionResult Project(SagaStatisticsSnapshot snapshot, SagaStatisticsEvent entry);
}

/// <summary>A durable, per-instance status observation with an ordered revision.</summary>
public sealed record SagaStatisticsEvent(
    string InstanceId,
    string OrchestrationKey,
    string Version,
    long Revision,
    OrchestrationInstanceStatus Status,
    DateTime StartedOnUtc,
    DateTime OccurredOnUtc);

/// <summary>The last applied state and statuses ever reached by an instance.</summary>
public sealed record SagaStatisticsSnapshot(
    string InstanceId,
    string OrchestrationKey,
    string Version,
    long Revision,
    OrchestrationInstanceStatus Status,
    DateTime StartedOnUtc,
    long ReachedStatuses);

/// <summary>A signed change to the count of instances currently in a status.</summary>
public sealed record SagaStatusCountDelta(OrchestrationInstanceStatus Status, int Change);

/// <summary>The outcome of applying one journal entry.</summary>
public enum SagaStatisticsProjectionDisposition { Applied, Duplicate, Gap }

/// <summary>Projection changes to persist atomically with the entry checkpoint.</summary>
public sealed record SagaStatisticsProjectionResult(
    SagaStatisticsProjectionDisposition Disposition,
    SagaStatisticsSnapshot Snapshot,
    IReadOnlyList<SagaStatusCountDelta> CurrentStatusDeltas,
    OrchestrationInstanceStatus? NewlyReachedStatus,
    bool StatusChanged);
