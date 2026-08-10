using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents a persisted domain catalog entry.
/// </summary>
public sealed class DomainEntity
{
    /// <summary>
    /// Gets or sets identifier.
    /// </summary>
    public Id Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "key")]
    /// <summary>
    /// Gets or sets unique key.
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

    [Sieve(CanFilter = true, CanSort = true, Name = "createdOnUtc")]
    /// <summary>
    /// Gets or sets created timestamp.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "updatedOnUtc")]
    /// <summary>
    /// Gets or sets updated timestamp.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }
}
