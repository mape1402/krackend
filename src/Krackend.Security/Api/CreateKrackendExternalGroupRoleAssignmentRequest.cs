namespace Krackend.Security.Api;

/// <summary>
/// Request used to create an external group role assignment.
/// </summary>
public sealed class CreateKrackendExternalGroupRoleAssignmentRequest
{
    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authorization scope type.
    /// </summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authorization scope identifier.
    /// </summary>
    public string ScopeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assignment source.
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
