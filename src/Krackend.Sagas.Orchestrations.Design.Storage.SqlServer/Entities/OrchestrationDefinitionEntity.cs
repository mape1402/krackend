using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Entities;

/// <summary>
/// Represents OrchestrationDefinitionEntity.
/// </summary>
public sealed class OrchestrationDefinitionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "key")]
    /// <summary>
    /// Gets or sets Key.
    /// </summary>
    public string Key { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "name")]
    /// <summary>
    /// Gets or sets Name.
    /// </summary>
    public string Name { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "domain")]
    /// <summary>
    /// Gets or sets Domain.
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// Gets or sets referenced domain identifier.
    /// </summary>
    public Id? DomainId { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "isActive")]
    /// <summary>
    /// Gets or sets IsActive.
    /// </summary>
    public bool IsActive { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "createdOnUtc")]
    /// <summary>
    /// Gets or sets CreatedOnUtc.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets CreatedBy.
    /// </summary>
    public string CreatedBy { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "updatedOnUtc")]
    /// <summary>
    /// Gets or sets UpdatedOnUtc.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets UpdatedBy.
    /// </summary>
    public string UpdatedBy { get; set; }
    /// <summary>
    /// Gets or sets Description.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets OwnerTeam.
    /// </summary>
    public string OwnerTeam { get; set; }
    /// <summary>
    /// Gets or sets owner team projection id.
    /// </summary>
    public Id? OwnerTeamId { get; set; }
    /// <summary>
    /// Gets or sets Tags.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets referenced domain.
    /// </summary>
    public DomainEntity DomainRef { get; set; }
    /// <summary>
    /// Gets or sets referenced owner team projection.
    /// </summary>
    public TeamProjectionEntity OwnerTeamRef { get; set; }
}
