namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>Optionally distributes runtime notifications between replicas.</summary>
public interface IRuntimeReactiveFanout
{
    /// <summary>Raised for events originating on a different replica.</summary>
    event Action<RuntimeReactiveEvent> Received;

    /// <summary>Raised when subscribers must reconcile after an interruption.</summary>
    event Action ResyncRequired;

    /// <summary>Distributes an event without being a source of truth for runtime state.</summary>
    Task PublishAsync(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default);
}
