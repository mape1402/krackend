namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using System.Text.Json.Nodes;

/// <summary>
/// Represents task execution attempt in the orchestrator domain.
/// </summary>
public sealed class TaskExecutionAttempt
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets task execution id.
    /// </summary>
    public Id TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets attempt number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public TaskExecutionStatus Status { get; set; }

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
    /// Gets or sets request payload.
    /// </summary>
    public JsonNode RequestPayload { get; set; }

    /// <summary>
    /// Gets or sets response payload.
    /// </summary>
    public JsonNode ResponsePayload { get; set; }

    /// <summary>
    /// Gets or sets error code.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets error message.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets dispatch id.
    /// </summary>
    public Id? DispatchId { get; set; }

    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
