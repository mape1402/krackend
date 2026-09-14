namespace Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents a security team that can own orchestrations.
/// </summary>
public sealed class Team
{
    /// <summary>
    /// Gets or sets team identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets unique key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets active flag.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets created timestamp in UTC.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated timestamp in UTC.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }
}
