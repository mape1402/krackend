namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

/// <summary>
/// Canonical transport-neutral output that must be dispatched by durable runtime work.
/// </summary>
public sealed class RuntimeDispatchEnvelope
{
    /// <summary>
    /// Gets or sets the dispatch id.
    /// </summary>
    public required string DispatchId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance id.
    /// </summary>
    public required string OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the execution key used for diagnostics.
    /// </summary>
    public string ExecutionKey { get; set; }

    /// <summary>
    /// Gets or sets the end-to-end correlation id.
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the business saga id.
    /// </summary>
    public string SagaId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration name.
    /// </summary>
    public required string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the orchestration version.
    /// </summary>
    public required string OrchestrationVersion { get; set; }

    /// <summary>
    /// Gets or sets the stage key.
    /// </summary>
    public required string StageKey { get; set; }

    /// <summary>
    /// Gets or sets the task key.
    /// </summary>
    public required string TaskKey { get; set; }

    /// <summary>
    /// Gets or sets the task execution id.
    /// </summary>
    public required string TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the execution attempt number.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets how the task must be dispatched.
    /// </summary>
    public RuntimeTransportDescriptor Destination { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the runtime expects a task response.
    /// </summary>
    public bool ExpectedResponse { get; set; } = true;

    /// <summary>
    /// Gets or sets the dispatch payload.
    /// </summary>
    public required JsonNode Payload { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets when the dispatch was created.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
}
