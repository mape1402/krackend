namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Represents a team member addition request.
/// </summary>
public sealed class AddTeamMemberRequest
{
    /// <summary>
    /// Gets or sets the external user identifier.
    /// </summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
