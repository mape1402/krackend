namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents orchestration instance in the orchestrator domain.
/// </summary>
public sealed class OrchestrationInstance
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets environment key.
    /// </summary>
    public required string EnvironmentKey { get; set; }

    /// <summary>
    /// Gets or sets orchestration definition key.
    /// </summary>
    public required string OrchestrationDefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets runtime orchestration artifact id.
    /// </summary>
    public Id RuntimeOrchestrationArtifactId { get; set; }

    /// <summary>
    /// Gets or sets trigger intake id.
    /// </summary>
    public Id TriggerIntakeId { get; set; }

    /// <summary>
    /// Gets or sets correlation id.
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets execution key.
    /// </summary>
    public required string ExecutionKey { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public OrchestrationInstanceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets current stage key.
    /// </summary>
    public string CurrentStageKey { get; set; }

    /// <summary>
    /// Gets or sets current task key.
    /// </summary>
    public string CurrentTaskKey { get; set; }

    /// <summary>
    /// Gets or sets current parallel group key.
    /// </summary>
    public string CurrentParallelGroupKey { get; set; }

    /// <summary>
    /// Gets or sets started on utc.
    /// </summary>
    public DateTime StartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets last updated on utc.
    /// </summary>
    public DateTime LastUpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets waiting since utc.
    /// </summary>
    public DateTime? WaitingSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets completed on utc.
    /// </summary>
    public DateTime? CompletedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets failed on utc.
    /// </summary>
    public DateTime? FailedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets stopped on utc.
    /// </summary>
    public DateTime? StoppedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets compensation started on utc.
    /// </summary>
    public DateTime? CompensationStartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets compensated on utc.
    /// </summary>
    public DateTime? CompensatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets final outcome.
    /// </summary>
    public string FinalOutcome { get; set; }

    /// <summary>
    /// Gets or sets error summary.
    /// </summary>
    public string ErrorSummary { get; set; }

    /// <summary>
    /// Gets or sets retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets active lease id.
    /// </summary>
    public string ActiveLeaseId { get; set; }

    /// <summary>
    /// Gets or sets active lease expires on utc.
    /// </summary>
    public DateTime? ActiveLeaseExpiresOnUtc { get; set; }

    /// <summary>
    /// Gets or sets snapshot payload.
    /// </summary>
    public JsonNode SnapshotPayload { get; set; }

    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
