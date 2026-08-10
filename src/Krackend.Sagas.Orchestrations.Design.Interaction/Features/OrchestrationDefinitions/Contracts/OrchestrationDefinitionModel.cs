namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction data for orchestration definition.
/// </summary>
public sealed class OrchestrationDefinitionModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the domain.
    /// </summary>
    public string Domain { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets domain identifier.
    /// </summary>
    public string DomainId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets domain display name.
    /// </summary>
    public string DomainDisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the owner team.
    /// </summary>
    public string OwnerTeam { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the owner team id.
    /// </summary>
    public string OwnerTeamId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the owner team display name.
    /// </summary>
    public string OwnerTeamDisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the tags.
    /// </summary>
    public IEnumerable<string> Tags { get; set; } = Array.Empty<string>();
    /// <summary>
    /// Gets or sets the is active.
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// Gets or sets the created on utc.
    /// </summary>
    public string CreatedOnUtc { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the created by.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the updated on utc.
    /// </summary>
    public string UpdatedOnUtc { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the updated by.
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}


