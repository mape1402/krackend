using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Dispatches queued runtime reactive events to SignalR groups.
/// </summary>
public sealed class SignalRRuntimeReactiveEventDispatcher : BackgroundService
{
    private readonly SignalRRuntimeReactiveEventQueue _queue;
    private readonly IHubContext<RuntimeReactiveHub> _hubContext;
    private readonly ILogger<SignalRRuntimeReactiveEventDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRRuntimeReactiveEventDispatcher"/> class.
    /// </summary>
    public SignalRRuntimeReactiveEventDispatcher(
        SignalRRuntimeReactiveEventQueue queue,
        IHubContext<RuntimeReactiveHub> hubContext,
        ILogger<SignalRRuntimeReactiveEventDispatcher> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs the SignalR event dispatch loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await Dispatch(message, stoppingToken);
        }
    }

    private async Task Dispatch(RuntimeReactiveEventMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var instanceGroup = RuntimeReactiveHubGroups.Instance(message.OrchestrationInstanceId);
            await _hubContext.Clients.Groups(RuntimeReactiveHubGroups.Runtime, instanceGroup)
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
