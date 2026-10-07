#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.Operations;

/// <summary>
/// Captures the latest health check result for a runtime dependency.
/// </summary>
/// <param name="Name">Dependency name.</param>
/// <param name="Kind">Dependency criticality.</param>
/// <param name="IsAvailable">Whether the dependency is available.</param>
/// <param name="Reason">Optional diagnostic reason.</param>
public sealed record RuntimeDependencyProbeResult(
    string Name,
    RuntimeDependencyKind Kind,
    bool IsAvailable,
    string? Reason = null);
