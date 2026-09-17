using Microsoft.AspNetCore.Authorization;

namespace Krackend.Security.AspNetCore;

/// <summary>
/// Represents an ASP.NET Core authorization requirement for a product permission.
/// </summary>
public sealed class KrackendPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendPermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">Required product permission.</param>
    /// <param name="scopeType">Required scope type.</param>
    /// <param name="scopeId">Required scope identifier.</param>
    public KrackendPermissionRequirement(string permission, string scopeType, string scopeId = "")
    {
        Permission = permission ?? string.Empty;
        ScopeType = scopeType ?? string.Empty;
        ScopeId = scopeId ?? string.Empty;
    }

    /// <summary>
    /// Gets the required product permission.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Gets the required scope type.
    /// </summary>
    public string ScopeType { get; }

    /// <summary>
    /// Gets the required scope identifier.
    /// </summary>
    public string ScopeId { get; }
}
