namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents stage execution in the orchestrator domain.
/// </summary>
public sealed class StageExecution
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
    /// Gets or sets stage key.
    /// </summary>
    public required string StageKey { get; set; }

    /// <summary>
    /// Gets or sets order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public StageExecutionStatus Status { get; set; }

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
    /// Gets or sets started on utc.
    /// </summary>
    public DateTime? StartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets completed on utc.
    /// </summary>
    public DateTime? CompletedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets failed on utc.
    /// </summary>
    public DateTime? FailedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets error summary.
    /// </summary>
    public string ErrorSummary { get; set; }

    /// <summary>
    /// Gets or sets parallel group count.
    /// </summary>
    public int ParallelGroupCount { get; set; }
    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}

