namespace Krackend.Sagas.Orchestrations.Runtime.Engine;

/// <summary>
/// Processes callbacks that are already waiting for the same orchestration instance
/// under one instance lease and one durable state transaction.
/// </summary>
public interface IBatchedSagaEngine
{
    /// <summary>
    /// Applies a set of callbacks for one orchestration instance atomically.
    /// </summary>
    Task OrchestrateBatchAsync(
        IReadOnlyCollection<ForwardIntent> intents,
        CancellationToken cancellationToken = default);
}
