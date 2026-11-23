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

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRRuntimeReactiveEventPublisher"/> class.
    /// </summary>
    public SignalRRuntimeReactiveEventPublisher(SignalRRuntimeReactiveEventQueue queue, IRuntimeReactiveFanout fanout = null,
        ILogger<SignalRRuntimeReactiveEventPublisher> logger = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _fanout = fanout;
    }

    /// <summary>
    /// Enqueues a runtime reactive event for SignalR delivery.
    /// </summary>
    public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null)
            return Task.CompletedTask;

        _queue.TryEnqueue(RuntimeReactiveEventMessage.From(eventData));
        // Optional diagnostics must not retain a saga lease while Redis is slow.
        // Local SignalR delivery and remote fanout have independent bounded readers.
        if (_fanout is not null) _queue.TryEnqueueFanout(eventData);
        return Task.CompletedTask;
    }
}
