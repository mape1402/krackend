using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>
/// Event emitted by the runtime plane when an execution transition becomes visible.
/// </summary>
public sealed class RuntimeReactiveEvent
{
    /// <summary>
    /// Gets or sets event id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets the canonical reactive event name.
    /// </summary>
    public required string EventName { get; set; }

    /// <summary>
    /// Gets or sets the persisted transition type that produced this event.
    /// </summary>
    public required string TransitionType { get; set; }

    /// <summary>
    /// Gets or sets orchestration definition key.
    /// </summary>
    public required string OrchestrationDefinitionKey { get; set; }

    /// <summary>
    /// Gets or sets orchestration version.
    /// </summary>
    public string OrchestrationVersion { get; set; }

    /// <summary>
    /// Gets or sets orchestration instance id.
    /// </summary>
    public Id OrchestrationInstanceId { get; set; }

    /// <summary>
    /// Gets or sets correlation id.
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets execution key.
    /// </summary>
    public required string ExecutionKey { get; set; }

    /// <summary>
    /// Gets or sets stage execution id.
    /// </summary>
    public Id? StageExecutionId { get; set; }

    /// <summary>
    /// Gets or sets stage key.
    /// </summary>
    public string StageKey { get; set; }

    /// <summary>
    /// Gets or sets task execution id.
    /// </summary>
    public Id? TaskExecutionId { get; set; }

    /// <summary>
    /// Gets or sets task key.
    /// </summary>
    public string TaskKey { get; set; }

    /// <summary>
    /// Gets or sets task attempt id.
    /// </summary>
    public Id? TaskExecutionAttemptId { get; set; }

    /// <summary>
    /// Gets or sets source status.
    /// </summary>
    public string FromStatus { get; set; }

    /// <summary>
    /// Gets or sets destination status.
    /// </summary>
    public string ToStatus { get; set; }

    /// <summary>
    /// Gets or sets current orchestration instance status.
    /// </summary>
    public required string InstanceStatus { get; set; }

    /// <summary>
    /// Gets or sets event time.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// Gets or sets message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets optional technical payload.
    /// </summary>
    public JsonNode Payload { get; set; }

    /// <summary>
    /// Gets or sets event producer.
    /// </summary>
    public required string ProducedBy { get; set; }
}
