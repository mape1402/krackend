namespace Krackend.Security.Core;

/// <summary>
/// Represents a permission assignment granted directly to an external subject.
/// </summary>
public sealed class KrackendPermissionAssignment
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
    /// Gets or sets the provider-specific subject identifier.
    /// </summary>
    public string SubjectId { get; set; } = string.Empty;

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
    public string Source { get; set; } = KrackendAssignmentSources.Manual;

    /// <summary>
    /// Gets or sets a value indicating whether the assignment is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC instant when the assignment was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
