using System.Security.Claims;

namespace Krackend.Security.Authorization;

/// <summary>
/// Evaluates product permissions for authenticated host users.
/// </summary>
public interface IKrackendAuthorizationService
{
    /// <summary>
    /// Checks whether a user has a permission in a scope.
    /// </summary>
    /// <param name="user">Authenticated host principal.</param>
    /// <param name="permission">Required product permission.</param>
    /// <param name="scope">Required authorization scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when access is granted.</returns>
    Task<bool> HasPermission(
        ClaimsPrincipal user,
        string permission,
        KrackendAuthorizationScope scope,
        CancellationToken cancellationToken = default);
}
