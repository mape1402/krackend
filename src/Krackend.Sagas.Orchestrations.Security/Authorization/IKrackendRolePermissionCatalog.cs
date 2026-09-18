namespace Krackend.Sagas.Orchestrations.Security.Authorization;

/// <summary>
/// Expands orchestration roles into orchestration permissions.
/// </summary>
public interface IKrackendRolePermissionCatalog
{
    /// <summary>
    /// Gets the permissions granted by a role.
    /// </summary>
    /// <param name="role">Role name.</param>
    /// <returns>Permissions granted by the role.</returns>
    IReadOnlyCollection<string> GetPermissions(string role);
}
