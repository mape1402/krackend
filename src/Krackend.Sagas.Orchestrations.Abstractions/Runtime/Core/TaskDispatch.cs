namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents task dispatch in the orchestrator domain.
/// </summary>
public sealed class TaskDispatch
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets task execution attempt id.
    /// </summary>
    public Id TaskExecutionAttemptId { get; set; }

    /// <summary>
    /// Gets or sets dispatch type.
    /// </summary>
    public required string DispatchType { get; set; }

    /// <summary>
    /// Gets or sets destination.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets request payload.
    /// </summary>
    public JsonNode RequestPayload { get; set; }

    /// <summary>
    /// Gets or sets dispatch status.
    /// </summary>
    public required string DispatchStatus { get; set; }

    /// <summary>
    /// Gets or sets command id.
    /// </summary>
    public string CommandId { get; set; }

    /// <summary>
    /// Gets or sets correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets sent on utc.
    /// </summary>
    public DateTime? SentOnUtc { get; set; }

    /// <summary>
    /// Gets or sets scheduled on utc.
    /// </summary>
    public DateTime? ScheduledOnUtc { get; set; }

    /// <summary>
    /// Gets or sets acknowledged on utc.
    /// </summary>
    public DateTime? AcknowledgedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets failed on utc.
    /// </summary>
    public DateTime? FailedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets failure reason.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
