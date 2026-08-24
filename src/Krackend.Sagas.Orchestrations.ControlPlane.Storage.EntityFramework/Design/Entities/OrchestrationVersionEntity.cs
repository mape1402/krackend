using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Sieve.Attributes;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Entities;

/// <summary>
/// Represents OrchestrationVersionEntity.
/// </summary>
public sealed class OrchestrationVersionEntity
{
    /// <summary>
    /// Gets or sets Id.
    /// </summary>
    public Id Id { get; set; }
    /// <summary>
    /// Gets or sets OrchestrationDefinitionId.
    /// </summary>
    public Id OrchestrationDefinitionId { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "version")]
    /// <summary>
    /// Gets or sets Version.
    /// </summary>
    public string Version { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "status")]
    /// <summary>
    /// Gets or sets Status.
    /// </summary>
    public OrchestrationVersionStatus Status { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "versionLabel")]
    /// <summary>
    /// Gets or sets VersionLabel.
    /// </summary>
    public string VersionLabel { get; set; }

    /// <summary>
    /// Gets or sets Description.
    /// </summary>
    public string Description { get; set; }
    /// <summary>
    /// Gets or sets Checksum.
    /// </summary>
    public string Checksum { get; set; }
    /// <summary>
    /// Gets or sets Notes.
    /// </summary>
    public string Notes { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "createdOnUtc")]
    /// <summary>
    /// Gets or sets CreatedOnUtc.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets CreatedBy.
    /// </summary>
    public string CreatedBy { get; set; }
    /// <summary>
    /// Gets or sets ApprovedOnUtc.
    /// </summary>
    public DateTime? ApprovedOnUtc { get; set; }
    /// <summary>
    /// Gets or sets ApprovedBy.
    /// </summary>
    public string ApprovedBy { get; set; }

    [Sieve(CanFilter = true, CanSort = true, Name = "updatedOnUtc")]
    /// <summary>
    /// Gets or sets UpdatedOnUtc.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets UpdatedBy.
    /// </summary>
    public string UpdatedBy { get; set; }
}
