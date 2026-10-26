using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Publishes runtime reactive events into the SignalR dispatch queue.
/// </summary>
public sealed class SignalRRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
{
    private readonly SignalRRuntimeReactiveEventQueue _queue;
    private readonly IRuntimeReactiveFanout _fanout;
    private readonly ILogger<SignalRRuntimeReactiveEventPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRRuntimeReactiveEventPublisher"/> class.
    /// </summary>
    public SignalRRuntimeReactiveEventPublisher(SignalRRuntimeReactiveEventQueue queue, IRuntimeReactiveFanout fanout = null,
        ILogger<SignalRRuntimeReactiveEventPublisher> logger = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _fanout = fanout;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a runtime reactive event for SignalR delivery.
    /// </summary>
    public async Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null)
            return;

        _queue.TryEnqueue(RuntimeReactiveEventMessage.From(eventData));
        if (_fanout is null) return;
        try { await _fanout.PublishAsync(eventData, cancellationToken); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Unable to distribute runtime event {EventId} to other replicas.", eventData.Id); }
    }
}
