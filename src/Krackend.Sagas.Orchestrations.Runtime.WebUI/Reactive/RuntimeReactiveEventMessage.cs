using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Represents the SignalR payload sent to runtime diagnostics clients.
/// </summary>
public sealed record RuntimeReactiveEventMessage(
    string Id,
    string EventName,
    string TransitionType,
    string OrchestrationDefinitionKey,
    string OrchestrationVersion,
    string OrchestrationInstanceId,
    string CorrelationId,
    string ExecutionKey,
    string StageExecutionId,
    string StageKey,
    string TaskExecutionId,
    string TaskKey,
    string TaskExecutionAttemptId,
    string FromStatus,
    string ToStatus,
    string InstanceStatus,
    DateTime OccurredOnUtc,
    string Message,
    object Payload,
    string ProducedBy)
{
    /// <summary>
    /// Creates a SignalR payload from a runtime reactive event.
    /// </summary>
    public static RuntimeReactiveEventMessage From(RuntimeReactiveEvent eventData)
        => new(
            eventData.Id.ToString(),
            eventData.EventName,
            eventData.TransitionType,
            eventData.OrchestrationDefinitionKey,
            eventData.OrchestrationVersion,
            eventData.OrchestrationInstanceId.ToString(),
            eventData.CorrelationId,
            eventData.ExecutionKey,
            eventData.StageExecutionId?.ToString(),
            eventData.StageKey,
            eventData.TaskExecutionId?.ToString(),
            eventData.TaskKey,
            eventData.TaskExecutionAttemptId?.ToString(),
            eventData.FromStatus,
            eventData.ToStatus,
            eventData.InstanceStatus,
            eventData.OccurredOnUtc,
            eventData.Message,
            eventData.Payload,
            eventData.ProducedBy);
}
