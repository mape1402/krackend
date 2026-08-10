namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents execution transition in the orchestrator domain.
/// </summary>
public sealed class ExecutionTransition
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
    public Id? StageExecutionId { get; set; }

    /// <summary>
    /// Gets or sets task execution id.
    /// </summary>
    public Id? TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets task execution attempt id.
    /// </summary>
    public Id? TaskExecutionAttemptId { get; set; }

    /// <summary>
    /// Gets or sets transition type.
    /// </summary>
    public required string TransitionType { get; set; }

    /// <summary>
    /// Gets or sets from status.
    /// </summary>
    public string FromStatus { get; set; }

    /// <summary>
    /// Gets or sets to status.
    /// </summary>
    public string ToStatus { get; set; }

    /// <summary>
    /// Gets or sets occurred on utc.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// Gets or sets message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets payload.
    /// </summary>
    public JsonNode Payload { get; set; }

    /// <summary>
    /// Gets or sets produced by.
    /// </summary>
    public string ProducedBy { get; set; }
}
