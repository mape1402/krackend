namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

/// <summary>Reports whether notifications can be exchanged between runtime replicas.</summary>
public interface IRuntimeReactiveFanoutHealth
{
    /// <summary>Whether the configured cross-replica notification channel is available.</summary>
    bool IsSynchronized { get; }

    /// <summary>Raised when cross-replica notification availability changes.</summary>
    event Action<bool> SynchronizationChanged;
}
