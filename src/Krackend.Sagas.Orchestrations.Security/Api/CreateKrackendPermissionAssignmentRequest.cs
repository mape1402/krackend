namespace Krackend.Sagas.Orchestrations.Security.Api;

/// <summary>
/// Request used to create a permission assignment.
/// </summary>
public sealed class CreateKrackendPermissionAssignmentRequest
{
    /// <summary>
    /// Gets or sets the permission name.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

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
