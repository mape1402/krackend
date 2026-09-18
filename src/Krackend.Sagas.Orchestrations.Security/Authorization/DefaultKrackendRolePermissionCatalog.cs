namespace Krackend.Sagas.Orchestrations.Security.Authorization;

/// <summary>
/// Provides the default Krackend role-to-permission mapping.
/// </summary>
public sealed class DefaultKrackendRolePermissionCatalog : IKrackendRolePermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> RolePermissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [KrackendRoles.Reader] =
            [
                KrackendPermissions.PortalAccess,
                KrackendPermissions.ControlPlaneRead,
                KrackendPermissions.RuntimeRead,
                KrackendPermissions.RuntimeInstancesRead,
            ],
            [KrackendRoles.Designer] =
            [
                KrackendPermissions.PortalAccess,
                KrackendPermissions.ControlPlaneRead,
                KrackendPermissions.ControlPlaneDesignWrite,
                KrackendPermissions.RuntimeRead,
                KrackendPermissions.RuntimeInstancesRead,
            ],
            [KrackendRoles.ReleaseManager] =
            [
                KrackendPermissions.PortalAccess,
                KrackendPermissions.ControlPlaneRead,
                KrackendPermissions.ControlPlaneReleaseExecute,
                KrackendPermissions.RuntimeRead,
                KrackendPermissions.RuntimeInstancesRead,
            ],
            [KrackendRoles.RuntimeOperator] =
            [
                KrackendPermissions.PortalAccess,
                KrackendPermissions.RuntimeRead,
                KrackendPermissions.RuntimeManage,
                KrackendPermissions.RuntimeArtifactsApply,
                KrackendPermissions.RuntimeInstancesRead,
            ],
            [KrackendRoles.SecurityAdmin] =
            [
                KrackendPermissions.PortalAccess,
                KrackendPermissions.ControlPlaneRead,
                KrackendPermissions.ControlPlaneSecurityManage,
                KrackendPermissions.RuntimeRead,
                KrackendPermissions.RuntimeInstancesRead,
            ],
            [KrackendRoles.Admin] = [KrackendPermissions.Wildcard],
        };

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetPermissions(string role)
        => string.IsNullOrWhiteSpace(role) || !RolePermissions.TryGetValue(role.Trim(), out var permissions)
            ? []
            : permissions;
}
