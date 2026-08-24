namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents the logical definition of an orchestration workflow.
/// </summary>
public sealed class OrchestrationDefinition
{
    /// <summary>
    /// Gets or sets id.
    /// </summary>
    public Id Id { get; set; }

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets domain.
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// Gets or sets referenced domain identifier.
    /// </summary>
    public Id? DomainId { get; set; }

    /// <summary>
    /// Gets or sets referenced domain display name.
    /// </summary>
    public string DomainDisplayName { get; set; }

    /// <summary>
    /// Gets or sets owner team.
    /// </summary>
    public string OwnerTeam { get; set; }

    /// <summary>
    /// Gets or sets owner team id.
    /// </summary>
    public Id? OwnerTeamId { get; set; }

    /// <summary>
    /// Gets or sets owner team display name.
    /// </summary>
    public string OwnerTeamDisplayName { get; set; }

    /// <summary>
    /// Gets or sets tags.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets created on utc.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets created by.
    /// </summary>
    public required string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets updated on utc.
    /// </summary>
    public DateTime? UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets updated by.
    /// </summary>
    public string UpdatedBy { get; set; }
}
