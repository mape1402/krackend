namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a domain catalog entry for orchestration definitions.
/// </summary>
public sealed class Domain
{
    /// <summary>
    /// Gets or sets domain identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets unique functional key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets optional description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the domain is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets last update timestamp in UTC.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }
}
