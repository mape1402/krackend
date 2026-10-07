namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Checks one dependency that contributes to the runtime operational state.
/// </summary>
public interface IRuntimeDependencyProbe
{
    /// <summary>
    /// Gets the dependency name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this dependency is critical or optional for runtime admission.
    /// </summary>
    RuntimeDependencyKind Kind { get; }

    /// <summary>
    /// Checks the dependency availability.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dependency probe result.</returns>
    ValueTask<RuntimeDependencyProbeResult> CheckAsync(CancellationToken cancellationToken = default);
}
