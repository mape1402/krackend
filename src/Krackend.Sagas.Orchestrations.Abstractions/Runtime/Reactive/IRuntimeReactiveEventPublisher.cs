namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>
/// Publishes runtime execution events to reactive consumers after the timeline is persisted.
/// </summary>
public interface IRuntimeReactiveEventPublisher
{
    /// <summary>
    /// Publishes a runtime execution event.
    /// </summary>
    /// <param name="eventData">Runtime event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default);
}
