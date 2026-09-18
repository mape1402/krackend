using Krackend.Sagas.Orchestrations.Security.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Krackend.Sagas.Orchestrations.Security.AspNetCore;

/// <summary>
/// Evaluates product permission requirements through the Krackend authorization service.
/// </summary>
public sealed class KrackendPermissionAuthorizationHandler : AuthorizationHandler<KrackendPermissionRequirement>
{
    private readonly IKrackendAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendPermissionAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="authorizationService">Product authorization service.</param>
    public KrackendPermissionAuthorizationHandler(IKrackendAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, KrackendPermissionRequirement requirement)
    {
        var scope = KrackendAuthorizationScope.Create(requirement.ScopeType, requirement.ScopeId);
        if (await _authorizationService.HasPermission(context.User, requirement.Permission, scope))
        {
            context.Succeed(requirement);
        }
    }
}
