namespace Krackend.Security.Core;

/// <summary>
/// Represents an authenticated external identity resolved from the host claims principal.
/// </summary>
public sealed class KrackendExternalSubject
{
    /// <summary>
    /// Gets or sets the external identity provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific subject identifier.
    /// </summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name resolved from claims.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email resolved from claims.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external group identifiers resolved from claims.
    /// </summary>
    public IReadOnlyCollection<string> GroupIds { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the external subject contains the required identity values.
    /// </summary>
    public bool IsResolved => !string.IsNullOrWhiteSpace(Provider) && !string.IsNullOrWhiteSpace(SubjectId);
}
