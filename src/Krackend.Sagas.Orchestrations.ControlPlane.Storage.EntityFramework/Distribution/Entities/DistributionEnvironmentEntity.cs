using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;

/// <summary>
/// Entity Framework representation of a Control Plane distribution environment.
/// </summary>
public sealed class DistributionEnvironmentEntity
{
    /// <summary>
    /// Gets or sets the environment id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets the environment display name.
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
    /// Gets or sets a value indicating whether the environment is enabled.
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

    /// <summary>
    /// Gets runtime nodes assigned to this environment.
    /// </summary>
    public ICollection<RuntimeNodeEntity> RuntimeNodes { get; set; } = new List<RuntimeNodeEntity>();
}
