namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Receives agnostic runtime operational state changes and lets adapters translate them to transport-specific behavior.
/// </summary>
public interface IRuntimeDegradationHandler
{
    /// <summary>
    /// Handles a runtime operational state change.
    /// </summary>
    /// <param name="context">State change context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous handler operation.</returns>
    ValueTask OnRuntimeStateChangedAsync(
        RuntimeOperationalStateChangedContext context,
        CancellationToken cancellationToken = default);
}
