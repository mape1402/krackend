using System.Text.Json.Nodes;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents task execution in the orchestrator domain.
/// </summary>
public sealed class TaskExecution
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets orchestration instance id.
    /// </summary>
    public Id OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets stage execution id.
    /// </summary>
    public Id StageExecutionId { get; set; }

    /// <summary>
    /// Gets or sets task key.
    /// </summary>
    public required string TaskKey { get; set; }

    /// <summary>
    /// Gets or sets task kind.
    /// </summary>
    public TaskKind TaskKind { get; set; }

    /// <summary>
    /// Gets or sets execution mode.
    /// </summary>
    public TaskExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// Gets or sets parallel group id.
    /// </summary>
    public Id? ParallelGroupId { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public TaskExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets was skipped.
    /// </summary>
    public bool WasSkipped { get; set; }

    /// <summary>
    /// Gets or sets skip reason.
    /// </summary>
    public string SkipReason { get; set; }

    /// <summary>
    /// Gets or sets execution condition result.
    /// </summary>
    public bool? ExecutionConditionResult { get; set; }

    /// <summary>
    /// Gets or sets on error policy.
    /// </summary>
    public OnErrorPolicy OnErrorPolicy { get; set; }

    /// <summary>
    /// Gets or sets await response.
    /// </summary>
    public bool AwaitResponse { get; set; }

    /// <summary>
    /// Gets or sets started on utc.
    /// </summary>
    public DateTime? StartedOnUtc { get; set; }

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
    /// Gets or sets timed out on utc.
    /// </summary>
    public DateTime? TimedOutOnUtc { get; set; }

    /// <summary>
    /// Gets or sets last attempt number.
    /// </summary>
    public int LastAttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets output variables payload.
    /// </summary>
    public JsonNode OutputVariablesPayload { get; set; }

    /// <summary>
    /// Gets or sets correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
