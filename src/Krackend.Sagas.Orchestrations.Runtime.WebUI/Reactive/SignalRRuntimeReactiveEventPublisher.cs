using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Publishes runtime reactive events into the SignalR dispatch queue.
/// </summary>
public sealed class SignalRRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
{
    private readonly SignalRRuntimeReactiveEventQueue _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRRuntimeReactiveEventPublisher"/> class.
    /// </summary>
    public SignalRRuntimeReactiveEventPublisher(SignalRRuntimeReactiveEventQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>
    /// Enqueues a runtime reactive event for SignalR delivery.
    /// </summary>
    public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null)
            return Task.CompletedTask;

        _queue.TryEnqueue(RuntimeReactiveEventMessage.From(eventData));
        return Task.CompletedTask;
    }
}
