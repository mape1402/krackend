namespace Krackend.Security.Configuration;

/// <summary>
/// Represents an externally authenticated subject that receives bootstrap administrator access.
/// </summary>
public sealed class KrackendBootstrapSubject
{
    /// <summary>
    /// Gets or sets the external identity provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider-specific subject identifier.
    /// </summary>
    public string SubjectId { get; set; } = string.Empty;
}
