using Krackend.Sagas.Orchestrations.Security.AspNetCore;
using Krackend.Sagas.Orchestrations.Security.Authorization;
using Krackend.Sagas.Orchestrations.Security.Configuration;
using Krackend.Sagas.Orchestrations.Security.Subjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Security.DependencyInjection;

/// <summary>
/// Registers provider-agnostic Krackend security services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Krackend orchestration authorization services.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSecurity(this IServiceCollection services)
        => services.AddKrackendSecurity(_ => { });

    /// <summary>
    /// Adds Krackend orchestration authorization services.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configure">Options callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSecurity(this IServiceCollection services, Action<KrackendSecurityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddScoped<IKrackendSubjectResolver, DefaultKrackendSubjectResolver>();
        services.TryAddSingleton<IKrackendRolePermissionCatalog, DefaultKrackendRolePermissionCatalog>();
        services.TryAddScoped<IKrackendAuthorizationService, KrackendAuthorizationService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, KrackendPermissionAuthorizationHandler>());
        services.AddAuthorization(options =>
        {
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.PortalAccess,
                KrackendPermissions.PortalAccess,
                KrackendAuthorizationScopeTypes.Global);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.ControlPlaneRead,
                KrackendPermissions.ControlPlaneRead,
                KrackendAuthorizationScopeTypes.ControlPlane);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.ControlPlaneDesignWrite,
                KrackendPermissions.ControlPlaneDesignWrite,
                KrackendAuthorizationScopeTypes.ControlPlane);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.ControlPlaneReleaseExecute,
                KrackendPermissions.ControlPlaneReleaseExecute,
                KrackendAuthorizationScopeTypes.ControlPlane);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.ControlPlaneSecurityManage,
                KrackendPermissions.ControlPlaneSecurityManage,
                KrackendAuthorizationScopeTypes.ControlPlane);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.RuntimeRead,
                KrackendPermissions.RuntimeRead,
                KrackendAuthorizationScopeTypes.Runtime);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.RuntimeManage,
                KrackendPermissions.RuntimeManage,
                KrackendAuthorizationScopeTypes.Runtime);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.RuntimeArtifactsApply,
                KrackendPermissions.RuntimeArtifactsApply,
                KrackendAuthorizationScopeTypes.Runtime);
            options.AddKrackendPermissionPolicy(
                KrackendAuthorizationPolicies.RuntimeInstancesRead,
                KrackendPermissions.RuntimeInstancesRead,
                KrackendAuthorizationScopeTypes.Runtime);
        });

        return services;
    }

    private static void AddKrackendPermissionPolicy(
        this AuthorizationOptions options,
        string policyName,
        string permission,
        string scopeType)
    {
        options.AddPolicy(
            policyName,
            policy => policy.Requirements.Add(new KrackendPermissionRequirement(permission, scopeType)));
    }
}
