namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Represents team projection interaction data.
/// </summary>
public sealed class TeamProjectionModel
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
    /// Gets or sets team display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets active state.
    /// </summary>
    public bool IsActive { get; set; }
}
