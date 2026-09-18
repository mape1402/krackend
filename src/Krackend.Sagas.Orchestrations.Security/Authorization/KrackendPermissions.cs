namespace Krackend.Sagas.Orchestrations.Security.Authorization;

/// <summary>
/// Defines stable Krackend product permission names.
/// </summary>
public static class KrackendPermissions
{
    /// <summary>
    /// Allows opening the Krackend portal shell.
    /// </summary>
    public const string PortalAccess = "krackend.portal.access";

    /// <summary>
    /// Allows reading control-plane data.
    /// </summary>
    public const string ControlPlaneRead = "krackend.control-plane.read";

    /// <summary>
    /// Allows mutating orchestration design data.
    /// </summary>
    public const string ControlPlaneDesignWrite = "krackend.control-plane.design.write";

    /// <summary>
    /// Allows creating releases and executing artifact delivery.
    /// </summary>
    public const string ControlPlaneReleaseExecute = "krackend.control-plane.release.execute";

    /// <summary>
    /// Allows administering orchestration authorization data.
    /// </summary>
    public const string ControlPlaneSecurityManage = "krackend.control-plane.security.manage";

    /// <summary>
    /// Allows reading runtime diagnostics and runtime configuration.
    /// </summary>
    public const string RuntimeRead = "krackend.runtime.read";

    /// <summary>
    /// Allows managing runtime operational state.
    /// </summary>
    public const string RuntimeManage = "krackend.runtime.manage";

    /// <summary>
    /// Allows applying or reapplying runtime artifacts.
    /// </summary>
    public const string RuntimeArtifactsApply = "krackend.runtime.artifacts.apply";

    /// <summary>
    /// Allows reading orchestration instance diagnostics.
    /// </summary>
    public const string RuntimeInstancesRead = "krackend.runtime.instances.read";

    /// <summary>
    /// Grants every product permission.
    /// </summary>
    public const string Wildcard = "*";

    /// <summary>
    /// Gets all stable permission names.
    /// </summary>
    public static IReadOnlyCollection<string> All { get; } =
    [
        PortalAccess,
        ControlPlaneRead,
        ControlPlaneDesignWrite,
        ControlPlaneReleaseExecute,
        ControlPlaneSecurityManage,
        RuntimeRead,
        RuntimeManage,
        RuntimeArtifactsApply,
        RuntimeInstancesRead,
    ];
}
