using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

/// <summary>
/// Represents a logical deployment environment in the Control Plane.
/// </summary>
public sealed class DistributionEnvironment
{
    /// <summary>
    /// Gets or sets the environment id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the stable environment code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the environment can receive new runtime nodes or releases.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets when the environment was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the environment was last updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
