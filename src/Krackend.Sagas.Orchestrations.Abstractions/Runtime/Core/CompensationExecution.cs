
namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents compensation execution in the orchestrator domain.
/// </summary>
public sealed class CompensationExecution
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
    /// Gets or sets source task execution id.
    /// </summary>
    public Id SourceTaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets compensation task key.
    /// </summary>
    public required string CompensationTaskKey { get; set; }

    /// <summary>
    /// Gets or sets status.
    /// </summary>
    public required string Status { get; set; }

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
    /// Gets or sets request payload.
    /// </summary>
    public JsonNode RequestPayload { get; set; }

    /// <summary>
    /// Gets or sets response payload.
    /// </summary>
    public JsonNode ResponsePayload { get; set; }

    /// <summary>
    /// Gets or sets error message.
    /// </summary>
    public string ErrorMessage { get; set; }
    /// <summary>
    /// Gets or sets metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}

