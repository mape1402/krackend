using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Entities;

/// <summary>
/// Represents persisted team entity.
/// </summary>
public sealed class TeamEntity
{
    /// <summary>
    /// Gets or sets identifier.
    /// </summary>
    public Id Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "key")]
    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public string Key { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "displayName")]
    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public string DisplayName { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "description")]
    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "isActive")]
    /// <summary>
    /// Gets or sets active flag.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets created timestamp.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated timestamp.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets team members navigation.
    /// </summary>
    public ICollection<TeamMemberEntity> Members { get; set; } = [];
}
