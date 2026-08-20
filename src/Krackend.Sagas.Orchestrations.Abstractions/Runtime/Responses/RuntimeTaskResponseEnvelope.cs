namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Responses;

using System.Text.Json.Nodes;

/// <summary>
/// Carries a service task response back to the orchestration runtime.
/// </summary>
public sealed class RuntimeTaskResponseEnvelope
{
    /// <summary>
    /// Gets or sets whether the task operation succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the operation status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the business saga id.
    /// </summary>
    public string SagaId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance id.
    /// </summary>
    public string OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the stage key.
    /// </summary>
    public string StageKey { get; set; }

    /// <summary>
    /// Gets or sets the task key.
    /// </summary>
    public string TaskKey { get; set; }

    /// <summary>
    /// Gets or sets the task execution id.
    /// </summary>
    public string TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the dispatch id.
    /// </summary>
    public string DispatchId { get; set; }

    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the request type name.
    /// </summary>
    public string RequestType { get; set; }

    /// <summary>
    /// Gets or sets the response type name.
    /// </summary>
    public string ResponseType { get; set; }

    /// <summary>
    /// Gets or sets the observed execution time in milliseconds.
    /// </summary>
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the business response payload.
    /// </summary>
    public JsonNode Payload { get; set; }

    /// <summary>
    /// Gets or sets the failure information when the operation failed.
    /// </summary>
    public RuntimeTaskResponseError Error { get; set; }

    /// <summary>
    /// Gets or sets additional response metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();
}
