using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents Design-local projection for security teams.
/// </summary>
public sealed class TeamProjectionEntity
{
    /// <summary>
    /// Gets or sets team id.
    /// </summary>
    public Id Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "key")]
    /// <summary>
    /// Gets or sets team key.
    /// </summary>
    public string Key { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "displayName")]
    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public string DisplayName { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "isActive")]
    /// <summary>
    /// Gets or sets active state.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets updated timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }
}
