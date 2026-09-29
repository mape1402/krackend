using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;

/// <summary>
/// Represents a persisted orchestration metadata descriptor.
/// </summary>
public sealed class MetadataDescriptorEntity
{
    /// <summary>
    /// Gets or sets identifier.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets unique metadata key.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets incoming message metadata key.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "sourceKey")]
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets JSON schema payload.
    /// </summary>
    public string SchemaJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets deterministic schema content hash.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets created timestamp.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "createdOnUtc")]
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated timestamp.
    /// </summary>
    [Sieve(CanFilter = true, CanSort = true, Name = "updatedOnUtc")]
    public DateTime? UpdatedOnUtc { get; set; }
}
