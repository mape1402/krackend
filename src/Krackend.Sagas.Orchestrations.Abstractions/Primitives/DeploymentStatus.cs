namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the deployment status values.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Represents pending.
    /// </summary>
    Pending,
    /// <summary>
    /// Represents downloaded.
    /// </summary>
    Downloaded,
    /// <summary>
    /// Represents validated.
    /// </summary>
    Validated,
    /// <summary>
    /// Represents persisted.
    /// </summary>
    Persisted,
    /// <summary>
    /// Represents activated.
    /// </summary>
    Activated,
    /// <summary>
    /// Represents failed.
    /// </summary>
    Failed,
    /// <summary>
    /// Represents rolled back.
    /// </summary>
    RolledBack,
    /// <summary>
    /// Represents retired.
    /// </summary>
    Retired
}
