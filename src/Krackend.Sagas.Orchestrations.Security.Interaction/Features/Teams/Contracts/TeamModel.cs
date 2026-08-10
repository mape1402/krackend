namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Represents team interaction data.
/// </summary>
public sealed class TeamModel
{
    /// <summary>
    /// Gets or sets team id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets team key.
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
    /// Gets or sets active state.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets member count.
    /// </summary>
    public int MemberCount { get; set; }
}
