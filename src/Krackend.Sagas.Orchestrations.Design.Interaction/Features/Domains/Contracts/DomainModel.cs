namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents interaction data for domain catalog entries.
/// </summary>
public sealed class DomainModel
{
    /// <summary>
    /// Gets or sets identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets active flag.
    /// </summary>
    public bool IsActive { get; set; }
}
