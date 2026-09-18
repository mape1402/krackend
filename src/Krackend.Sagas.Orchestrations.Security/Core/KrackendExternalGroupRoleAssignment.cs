namespace Krackend.Sagas.Orchestrations.Security.Core;

/// <summary>
/// Represents a role assignment granted to an external identity-provider group.
/// </summary>
public sealed class KrackendExternalGroupRoleAssignment
{
    /// <summary>
    /// Gets or sets the assignment identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external identity provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external group identifier.
    /// </summary>
    public string ExternalGroupId { get; set; } = string.Empty;

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
    public string Source { get; set; } = KrackendAssignmentSources.ExternalGroup;

    /// <summary>
    /// Gets or sets a value indicating whether the assignment is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC instant when the assignment was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
