using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class OrchestrationInstanceEntity
{
    public Id Id { get; set; }
    public string EnvironmentKey { get; set; }
    public string OrchestrationDefinitionKey { get; set; }
    public Id RuntimeOrchestrationArtifactId { get; set; }
    public Id TriggerIntakeId { get; set; }
    public string CorrelationId { get; set; }
    public string ExecutionKey { get; set; }
    public OrchestrationInstanceStatus Status { get; set; }
    public string CurrentStageKey { get; set; }
    public string CurrentTaskKey { get; set; }
    public string CurrentParallelGroupKey { get; set; }
    public DateTime StartedOnUtc { get; set; }
    public DateTime LastUpdatedOnUtc { get; set; }
    public DateTime? WaitingSinceUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public DateTime? FailedOnUtc { get; set; }
    public DateTime? StoppedOnUtc { get; set; }
    public DateTime? CompensationStartedOnUtc { get; set; }
    public DateTime? CompensatedOnUtc { get; set; }
    public string FinalOutcome { get; set; }
    public string ErrorSummary { get; set; }
    public int RetryCount { get; set; }
    public string ActiveLeaseId { get; set; }
    public DateTime? ActiveLeaseExpiresOnUtc { get; set; }
    public string SnapshotPayloadJson { get; set; }
    public string MetadataJson { get; set; }
}
