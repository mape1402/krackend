namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents local projection of security teams for Design consumption.
/// </summary>
public sealed class TeamProjection
{
    /// <summary>
    /// Gets or sets team id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets active state.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets updated timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
