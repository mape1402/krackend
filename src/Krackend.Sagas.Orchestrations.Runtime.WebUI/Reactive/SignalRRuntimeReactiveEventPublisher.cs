using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Microsoft.AspNetCore.SignalR;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

public sealed class SignalRRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
{
    private readonly IHubContext<RuntimeReactiveHub> _hubContext;

    public SignalRRuntimeReactiveEventPublisher(IHubContext<RuntimeReactiveHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null)
            return;

        var environmentGroup = RuntimeReactiveHubGroups.Environment(eventData.EnvironmentKey);
        var instanceGroup = RuntimeReactiveHubGroups.Instance(eventData.OrchestrationInstanceId.ToString());

        await _hubContext.Clients.Groups(environmentGroup, instanceGroup)
            .SendAsync("runtime.transition", RuntimeReactiveEventMessage.From(eventData), cancellationToken);
    }
}

internal sealed record RuntimeReactiveEventMessage(
    string Id,
    string EventName,
    string TransitionType,
    string EnvironmentKey,
    string OrchestrationDefinitionKey,
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
    public static RuntimeReactiveEventMessage From(RuntimeReactiveEvent eventData)
        => new(
            eventData.Id.ToString(),
            eventData.EventName,
            eventData.TransitionType,
            eventData.EnvironmentKey,
            eventData.OrchestrationDefinitionKey,
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
