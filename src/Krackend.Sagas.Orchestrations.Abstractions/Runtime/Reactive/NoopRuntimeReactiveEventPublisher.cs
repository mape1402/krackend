namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>
/// Reactive publisher used when no real-time runtime observer is configured.
/// </summary>
public sealed class NoopRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
{
    /// <inheritdoc/>
    public Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
