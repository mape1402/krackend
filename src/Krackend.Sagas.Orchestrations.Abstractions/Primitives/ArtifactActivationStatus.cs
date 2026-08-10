namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the artifact activation status values.
/// </summary>
public enum ArtifactActivationStatus
{
    /// <summary>
    /// Represents inactive.
    /// </summary>
    Inactive,
    /// <summary>
    /// Represents active.
    /// </summary>
    Active,
    /// <summary>
    /// Represents superseded.
    /// </summary>
    Superseded,
    /// <summary>
    /// Represents disabled.
    /// </summary>
    Disabled
}
