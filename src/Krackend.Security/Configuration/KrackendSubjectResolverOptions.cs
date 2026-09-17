using System.Security.Claims;

namespace Krackend.Security.Configuration;

/// <summary>
/// Configures claim-based subject resolution.
/// </summary>
public sealed class KrackendSubjectResolverOptions
{
    /// <summary>
    /// Gets or sets the external identity provider name assigned to resolved subjects.
    /// </summary>
    public string Provider { get; set; } = "default";

    /// <summary>
    /// Gets the claim types used to resolve the external subject identifier.
    /// </summary>
    public List<string> SubjectIdClaimTypes { get; } =
    [
        "oid",
        ClaimTypes.NameIdentifier,
        "sub",
    ];

    /// <summary>
    /// Gets the claim types used to resolve the subject email address.
    /// </summary>
    public List<string> EmailClaimTypes { get; } =
    [
        "preferred_username",
        ClaimTypes.Email,
        "email",
    ];

    /// <summary>
    /// Gets the claim types used to resolve the subject display name.
    /// </summary>
    public List<string> DisplayNameClaimTypes { get; } =
    [
        ClaimTypes.Name,
        "name",
    ];

    /// <summary>
    /// Gets the claim types used to resolve external group identifiers.
    /// </summary>
    public List<string> GroupClaimTypes { get; } =
    [
        "groups",
        ClaimTypes.GroupSid,
    ];
}
