using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Dispatches queued runtime reactive events to SignalR groups.
/// </summary>
public sealed class SignalRRuntimeReactiveEventDispatcher : BackgroundService
{
    private readonly SignalRRuntimeReactiveEventQueue _queue;
    private readonly IHubContext<RuntimeReactiveHub> _hubContext;
    private readonly ILogger<SignalRRuntimeReactiveEventDispatcher> _logger;
    private readonly IRuntimeReactiveFanout _fanout;
    private readonly IRuntimeStatisticsFanout _statisticsFanout;
    private readonly IRuntimeReactiveFanoutHealth _fanoutHealth;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRRuntimeReactiveEventDispatcher"/> class.
    /// </summary>
    public SignalRRuntimeReactiveEventDispatcher(
        SignalRRuntimeReactiveEventQueue queue,
        IHubContext<RuntimeReactiveHub> hubContext,
        ILogger<SignalRRuntimeReactiveEventDispatcher> logger,
        IRuntimeReactiveFanout fanout = null,
        IRuntimeStatisticsFanout statisticsFanout = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fanout = fanout;
        _fanoutHealth = fanout as IRuntimeReactiveFanoutHealth;
        _statisticsFanout = statisticsFanout;
        if (_fanout is not null)
        {
            _fanout.Received += OnReceived;
            _fanout.ResyncRequired += OnResyncRequired;
        }
        if (_statisticsFanout is not null) _statisticsFanout.Updated += OnStatisticsUpdated;
        if (_fanoutHealth is not null) _fanoutHealth.SynchronizationChanged += OnSynchronizationChanged;
    }

    /// <summary>
    /// Runs the SignalR event dispatch loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await Dispatch(message, stoppingToken);
            if (_queue.TryTakeResynchronizationRequired())
            {
                await NotifyResync();
            }
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

    private void OnReceived(RuntimeReactiveEvent eventData)
    {
        if (eventData is not null) _queue.TryEnqueue(RuntimeReactiveEventMessage.From(eventData));
    }

    private void OnResyncRequired() => _ = NotifyResync();

    private void OnStatisticsUpdated() => _ = NotifyStatisticsUpdated();

    private void OnSynchronizationChanged(bool synchronized) => _ = NotifySynchronizationChanged(synchronized);

    private async Task NotifySynchronizationChanged(bool synchronized)
    {
        try { await _hubContext.Clients.Group(RuntimeReactiveHubGroups.Runtime).SendAsync("runtime.fanout.health", synchronized); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify runtime clients about replica synchronization."); }
    }

    private async Task NotifyStatisticsUpdated()
    {
        try { await _hubContext.Clients.Group(RuntimeReactiveHubGroups.Runtime).SendAsync("runtime.statistics.updated"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify runtime clients about statistics."); }
    }

    private async Task NotifyResync()
    {
        try { await _hubContext.Clients.Group(RuntimeReactiveHubGroups.Runtime).SendAsync("runtime.resync"); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to notify runtime clients to reconcile."); }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_fanout is not null)
        {
            _fanout.Received -= OnReceived;
            _fanout.ResyncRequired -= OnResyncRequired;
        }
        if (_statisticsFanout is not null) _statisticsFanout.Updated -= OnStatisticsUpdated;
        if (_fanoutHealth is not null) _fanoutHealth.SynchronizationChanged -= OnSynchronizationChanged;
        base.Dispose();
    }
}
