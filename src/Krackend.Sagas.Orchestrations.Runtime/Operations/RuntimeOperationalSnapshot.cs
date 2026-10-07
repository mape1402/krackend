#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Captures the current runtime operational state and dependency diagnostics.
/// </summary>
public sealed class RuntimeOperationalSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeOperationalSnapshot"/> class.
    /// </summary>
    public RuntimeOperationalSnapshot(
        RuntimeOperationalState state,
        DateTime observedOnUtc,
        IReadOnlyCollection<RuntimeDependencyProbeResult> dependencies,
        string? reason = null)
    {
        State = state;
        ObservedOnUtc = observedOnUtc;
        Dependencies = dependencies ?? Array.Empty<RuntimeDependencyProbeResult>();
        Reason = reason;
    }

    /// <summary>
    /// Gets the operational state.
    /// </summary>
    public RuntimeOperationalState State { get; }

    /// <summary>
    /// Gets when the snapshot was observed.
    /// </summary>
    public DateTime ObservedOnUtc { get; }

    /// <summary>
    /// Gets dependency probe results.
    /// </summary>
    public IReadOnlyCollection<RuntimeDependencyProbeResult> Dependencies { get; }

    /// <summary>
    /// Gets the state reason.
    /// </summary>
    public string? Reason { get; }
}
