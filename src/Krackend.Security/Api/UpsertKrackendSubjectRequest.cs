namespace Krackend.Security.Api;

/// <summary>
/// Request used to create or update a known product subject.
/// </summary>
public sealed class UpsertKrackendSubjectRequest
{
    /// <summary>
    /// Gets or sets the subject identifier.
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
    /// Gets or sets the subject display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the subject is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
