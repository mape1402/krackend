using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Represents the SignalR payload sent to runtime diagnostics clients.
/// </summary>
public sealed record RuntimeReactiveEventMessage(
    string Id,
    string Revision,
    string EventName,
    string TransitionType,
    string OrchestrationDefinitionKey,
    string OrchestrationVersion,
    string OrchestrationInstanceId,
    string CorrelationId,
    string SagaId,
    string ExecutionKey,
    string StageExecutionId,
    string StageKey,
    string TaskExecutionId,
    string TaskKey,
    string TaskExecutionAttemptId,
    string FromStatus,
    string ToStatus,
    string InstanceStatus,
    DateTime StartedOnUtc,
    DateTime LastUpdatedOnUtc,
    DateTime OccurredOnUtc,
    string Message,
    [property: JsonIgnore] object Payload,
    string ProducedBy,
    int? CompletedTaskCount = null,
    int? TotalTaskCount = null,
    string CurrentStageKey = null,
    string CurrentTaskKey = null,
    string LastExecutedStageKey = null,
    string LastExecutedTaskKey = null)
{
    /// <summary>
    /// Creates a SignalR payload from a runtime reactive event.
    /// </summary>
    public static RuntimeReactiveEventMessage From(RuntimeReactiveEvent eventData)
        => new(
            eventData.Id.ToString(),
            eventData.Revision,
            eventData.EventName,
            eventData.TransitionType,
            eventData.OrchestrationDefinitionKey,
            eventData.OrchestrationVersion,
            eventData.OrchestrationInstanceId.ToString(),
            eventData.CorrelationId,
            eventData.SagaId,
            eventData.ExecutionKey,
            eventData.StageExecutionId?.ToString(),
            eventData.StageKey,
            eventData.TaskExecutionId?.ToString(),
            eventData.TaskKey,
            eventData.TaskExecutionAttemptId?.ToString(),
            eventData.FromStatus,
            eventData.ToStatus,
            eventData.InstanceStatus,
            eventData.StartedOnUtc,
            eventData.LastUpdatedOnUtc,
            eventData.OccurredOnUtc,
            eventData.Message,
            // SignalR updates deliberately stay compact. The complete transition payload is
            // available only from the instance detail endpoints when the user asks for it.
            null,
            eventData.ProducedBy,
            eventData.CompletedTaskCount,
            eventData.TotalTaskCount,
            eventData.CurrentStageKey,
            eventData.CurrentTaskKey,
            eventData.LastExecutedStageKey,
            eventData.LastExecutedTaskKey);
}
