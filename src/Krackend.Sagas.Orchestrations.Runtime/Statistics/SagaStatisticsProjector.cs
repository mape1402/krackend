using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>Projects ordered instance observations without counting retries twice.</summary>
public sealed class SagaStatisticsProjector : ISagaStatisticsProjector
{
    /// <inheritdoc />
    public SagaStatisticsProjectionResult Project(SagaStatisticsSnapshot snapshot, SagaStatisticsEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.InstanceId) || string.IsNullOrWhiteSpace(entry.OrchestrationKey) || entry.Revision < 1 || !Enum.IsDefined(entry.Status))
            throw new ArgumentException("The statistics event has an invalid identity, revision or status.", nameof(entry));
        if (snapshot is not null && (snapshot.InstanceId != entry.InstanceId || snapshot.OrchestrationKey != entry.OrchestrationKey || snapshot.Version != entry.Version || snapshot.StartedOnUtc != entry.StartedOnUtc))
            throw new ArgumentException("The statistics event does not match its instance projection.", nameof(entry));

        var previousRevision = snapshot?.Revision ?? 0;
        if (entry.Revision <= previousRevision)
            return new SagaStatisticsProjectionResult(SagaStatisticsProjectionDisposition.Duplicate, snapshot, [], null, false);
        if (entry.Revision != previousRevision + 1)
            return new SagaStatisticsProjectionResult(SagaStatisticsProjectionDisposition.Gap, snapshot, [], null, false);

        var reachedFlag = 1L << (int)entry.Status;
        var reached = snapshot?.ReachedStatuses ?? 0;
        var newlyReached = (reached & reachedFlag) == 0 ? entry.Status : (OrchestrationInstanceStatus?)null;
        var changed = snapshot is null || snapshot.Status != entry.Status;
        IReadOnlyList<SagaStatusCountDelta> deltas = snapshot is null
            ? [new(entry.Status, 1)]
            : changed ? [new(snapshot.Status, -1), new(entry.Status, 1)] : [];
        var updated = new SagaStatisticsSnapshot(entry.InstanceId, entry.OrchestrationKey, entry.Version, entry.Revision,
            entry.Status, entry.StartedOnUtc, reached | reachedFlag);
        return new SagaStatisticsProjectionResult(SagaStatisticsProjectionDisposition.Applied, updated, deltas, newlyReached, changed);
    }
}
