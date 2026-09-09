namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

/// <summary>
/// Canonical transport-neutral input accepted by the orchestration runtime.
/// </summary>
public sealed class RuntimeIngressEnvelope
{
    /// <summary>
    /// Gets or sets the runtime ingress identifier.
    /// </summary>
    public string IngressId { get; set; }

    /// <summary>
    /// Gets or sets the ingress kind.
    /// </summary>
    public RuntimeIngressKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the trigger or orchestration key used to resolve the artifact.
    /// </summary>
    public required string OrchestrationName { get; set; }

    /// <summary>
    /// Gets or sets the requested orchestration version.
    /// </summary>
    public string OrchestrationVersion { get; set; }

    /// <summary>
    /// Gets or sets the end-to-end correlation id.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the business saga id.
    /// </summary>
    public string SagaId { get; set; }

    /// <summary>
    /// Gets or sets the orchestration instance id.
    /// </summary>
    public string OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the current stage key when available.
    /// </summary>
    public string StageKey { get; set; }

    /// <summary>
    /// Gets or sets the current task key when available.
    /// </summary>
    public string TaskKey { get; set; }

    /// <summary>
    /// Gets or sets the task execution id when available.
    /// </summary>
    public string TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the dispatch id when available.
    /// </summary>
    public string DispatchId { get; set; }

    /// <summary>
    /// Gets or sets the execution attempt number.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key used by durable ingress.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the input transport descriptor.
    /// </summary>
    public RuntimeTransportDescriptor Source { get; set; } = new();

    /// <summary>
    /// Gets or sets the input payload.
    /// </summary>
    public JsonNode Payload { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, JsonNode> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets when the input was received by the runtime.
    /// </summary>
    public DateTime ReceivedOnUtc { get; set; } = DateTime.UtcNow;
}
