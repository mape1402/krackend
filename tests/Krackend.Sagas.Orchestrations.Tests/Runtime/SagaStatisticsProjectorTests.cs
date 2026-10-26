namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;

public sealed class SagaStatisticsProjectorTests
{
    private readonly SagaStatisticsProjector _projector = new();
    private static readonly DateTime Started = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstObservationAddsOneCurrentInstanceAndOneCohortMilestone()
    {
        var result = _projector.Project(null, Event(1, OrchestrationInstanceStatus.Created));
        Assert.Equal(SagaStatisticsProjectionDisposition.Applied, result.Disposition);
        Assert.Equal(new SagaStatusCountDelta(OrchestrationInstanceStatus.Created, 1), Assert.Single(result.CurrentStatusDeltas));
        Assert.Equal(OrchestrationInstanceStatus.Created, result.NewlyReachedStatus);
        Assert.True(result.StatusChanged);
    }

    [Fact]
    public void StatusChangeMovesTheCurrentCountAndRecordsTheNewMilestone()
    {
        var first = _projector.Project(null, Event(1, OrchestrationInstanceStatus.Created));
        var result = _projector.Project(first.Snapshot, Event(2, OrchestrationInstanceStatus.Running));
        Assert.Equal([new SagaStatusCountDelta(OrchestrationInstanceStatus.Created, -1), new SagaStatusCountDelta(OrchestrationInstanceStatus.Running, 1)], result.CurrentStatusDeltas);
        Assert.Equal(OrchestrationInstanceStatus.Running, result.NewlyReachedStatus);
    }

    [Fact]
    public void RepeatObservationAdvancesRevisionWithoutChangingCounts()
    {
        var first = _projector.Project(null, Event(1, OrchestrationInstanceStatus.Running));
        var result = _projector.Project(first.Snapshot, Event(2, OrchestrationInstanceStatus.Running));
        Assert.Equal(2, result.Snapshot.Revision);
        Assert.Empty(result.CurrentStatusDeltas);
        Assert.Null(result.NewlyReachedStatus);
        Assert.False(result.StatusChanged);
    }

    [Fact]
    public void RecoveryFromDeadLetterMovesCurrentCountsWithoutRepeatingFunnelMilestones()
    {
        var running = _projector.Project(null, Event(1, OrchestrationInstanceStatus.Running));
        var dead = _projector.Project(running.Snapshot, Event(2, OrchestrationInstanceStatus.DeadLettered));
        var recovered = _projector.Project(dead.Snapshot, Event(3, OrchestrationInstanceStatus.Running));
        Assert.Equal([new SagaStatusCountDelta(OrchestrationInstanceStatus.DeadLettered, -1), new SagaStatusCountDelta(OrchestrationInstanceStatus.Running, 1)], recovered.CurrentStatusDeltas);
        Assert.Null(recovered.NewlyReachedStatus);
        Assert.True(recovered.StatusChanged);
    }

    [Fact]
    public void DuplicateAndOutOfOrderEntriesDoNotChangeTheProjection()
    {
        var first = _projector.Project(null, Event(1, OrchestrationInstanceStatus.Created));
        var duplicate = _projector.Project(first.Snapshot, Event(1, OrchestrationInstanceStatus.Created));
        var gap = _projector.Project(first.Snapshot, Event(3, OrchestrationInstanceStatus.Completed));
        Assert.Equal(SagaStatisticsProjectionDisposition.Duplicate, duplicate.Disposition);
        Assert.Equal(SagaStatisticsProjectionDisposition.Gap, gap.Disposition);
        Assert.Same(first.Snapshot, duplicate.Snapshot);
        Assert.Same(first.Snapshot, gap.Snapshot);
        Assert.Empty(duplicate.CurrentStatusDeltas);
        Assert.Empty(gap.CurrentStatusDeltas);
        Assert.Equal(SagaStatisticsProjectionDisposition.Gap, _projector.Project(null, Event(2, OrchestrationInstanceStatus.Created)).Disposition);
    }

    [Fact]
    public void InvalidEntriesAndMismatchedInstanceIdentityAreRejected()
    {
        var snapshot = _projector.Project(null, Event(1, OrchestrationInstanceStatus.Created)).Snapshot;
        Assert.Throws<ArgumentNullException>(() => _projector.Project(null, null));
        Assert.Throws<ArgumentException>(() => _projector.Project(null, Event(0, OrchestrationInstanceStatus.Created)));
        Assert.Throws<ArgumentException>(() => _projector.Project(null, Event(1, (OrchestrationInstanceStatus)999)));
        Assert.Throws<ArgumentException>(() => _projector.Project(null, Event(1, OrchestrationInstanceStatus.Created) with { InstanceId = " " }));
        Assert.Throws<ArgumentException>(() => _projector.Project(null, Event(1, OrchestrationInstanceStatus.Created) with { OrchestrationKey = "" }));
        Assert.Throws<ArgumentException>(() => _projector.Project(snapshot, Event(2, OrchestrationInstanceStatus.Running) with { InstanceId = "other" }));
        Assert.Throws<ArgumentException>(() => _projector.Project(snapshot, Event(2, OrchestrationInstanceStatus.Running) with { OrchestrationKey = "other" }));
        Assert.Throws<ArgumentException>(() => _projector.Project(snapshot, Event(2, OrchestrationInstanceStatus.Running) with { Version = "2" }));
        Assert.Throws<ArgumentException>(() => _projector.Project(snapshot, Event(2, OrchestrationInstanceStatus.Running) with { StartedOnUtc = Started.AddDays(1) }));
    }

    private static SagaStatisticsEvent Event(long revision, OrchestrationInstanceStatus status)
        => new("instance", "sales", "1.0", revision, status, Started, Started.AddSeconds(revision));
}
