namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

/// <summary>
/// Represents team member interaction data.
/// </summary>
public sealed class TeamMemberModel
{
    /// <summary>
    /// Gets or sets team member id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets team id.
    /// </summary>
    public string TeamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets external user id.
    /// </summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets created timestamp text.
    /// </summary>
    public string CreatedOnUtc { get; set; } = string.Empty;
}
