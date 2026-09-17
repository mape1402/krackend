namespace Krackend.Security.Authorization;

/// <summary>
/// Expands product roles into product permissions.
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
