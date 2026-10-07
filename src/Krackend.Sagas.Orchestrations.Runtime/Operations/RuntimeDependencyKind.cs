namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Describes how a runtime dependency affects admission.
/// </summary>
public enum RuntimeDependencyKind
{
    /// <summary>
    /// The runtime must close when this dependency is unavailable.
    /// </summary>
    Critical,

    /// <summary>
    /// The runtime may continue in degraded mode when this dependency is unavailable.
    /// </summary>
    Optional
}
