using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class SagaStatisticsObservation
{
    public string Id { get; set; }
    public Id InstanceId { get; set; }
    public Id ArtifactId { get; set; }
    public string OrchestrationKey { get; set; }
    public long Revision { get; set; }
    public OrchestrationInstanceStatus Status { get; set; }
    public string StageKey { get; set; }
    public DateTime StartedOnUtc { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public bool Processed { get; set; }
    public bool StatusChanged { get; set; }
    public bool StatusFirstReached { get; set; }
    public bool IsBackfill { get; set; }
}
