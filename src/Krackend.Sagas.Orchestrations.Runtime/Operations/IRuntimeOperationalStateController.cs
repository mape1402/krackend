namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Evaluates runtime dependencies and emits operational state changes.
/// </summary>
public interface IRuntimeOperationalStateController : IRuntimeOperationalStateProvider
{
    /// <summary>
    /// Evaluates all registered dependency probes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resulting operational snapshot.</returns>
    Task<RuntimeOperationalSnapshot> EvaluateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the runtime as recovering while durable reconciliation is running.
    /// </summary>
    /// <param name="reason">Recovery reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resulting operational snapshot.</returns>
    Task<RuntimeOperationalSnapshot> MarkRecoveringAsync(string reason, CancellationToken cancellationToken = default);
}
