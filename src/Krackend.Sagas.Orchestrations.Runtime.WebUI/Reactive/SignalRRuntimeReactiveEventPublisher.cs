using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

public sealed class SignalRRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
{
    private readonly SignalRRuntimeReactiveEventQueue _queue;

    public SignalRRuntimeReactiveEventPublisher(SignalRRuntimeReactiveEventQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null)
            return Task.CompletedTask;

        _queue.TryEnqueue(RuntimeReactiveEventMessage.From(eventData));
        return Task.CompletedTask;
    }
}

public sealed class SignalRRuntimeReactiveEventQueue
{
    private readonly Channel<RuntimeReactiveEventMessage> _channel = Channel.CreateUnbounded<RuntimeReactiveEventMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<RuntimeReactiveEventMessage> Reader => _channel.Reader;

    public bool TryEnqueue(RuntimeReactiveEventMessage message)
        => message is not null && _channel.Writer.TryWrite(message);
}

public sealed class SignalRRuntimeReactiveEventDispatcher : BackgroundService
{
    private readonly SignalRRuntimeReactiveEventQueue _queue;
    private readonly IHubContext<RuntimeReactiveHub> _hubContext;
    private readonly ILogger<SignalRRuntimeReactiveEventDispatcher> _logger;

    public SignalRRuntimeReactiveEventDispatcher(
        SignalRRuntimeReactiveEventQueue queue,
        IHubContext<RuntimeReactiveHub> hubContext,
        ILogger<SignalRRuntimeReactiveEventDispatcher> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
            await Dispatch(message, stoppingToken);
    }

    private async Task Dispatch(RuntimeReactiveEventMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var environmentGroup = RuntimeReactiveHubGroups.Environment(message.EnvironmentKey);
            var instanceGroup = RuntimeReactiveHubGroups.Instance(message.OrchestrationInstanceId);
            await _hubContext.Clients.Groups(environmentGroup, instanceGroup)
                .SendAsync("runtime.transition", message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Runtime reactive event dispatch failed.");
        }
    }
}

public sealed record RuntimeReactiveEventMessage(
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
