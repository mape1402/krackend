using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class SagaStatisticsProjection
{
    public string Id { get; set; }
    public Id ArtifactId { get; set; }
    public string OrchestrationKey { get; set; }
    public long Revision { get; set; }
    public OrchestrationInstanceStatus Status { get; set; }
    public DateTime StartedOnUtc { get; set; }
    public DateTime LastObservedOnUtc { get; set; }
    public long ReachedStatuses { get; set; }
}
