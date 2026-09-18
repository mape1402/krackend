namespace Krackend.Sagas.Orchestrations.Security.AspNetCore;

/// <summary>
/// Defines ASP.NET Core policy names used by Krackend authorization.
/// </summary>
public static class KrackendAuthorizationPolicies
{
    /// <summary>
    /// Policy for portal access.
    /// </summary>
    public const string PortalAccess = "Krackend.Portal.Access";

    /// <summary>
    /// Policy for reading control-plane data.
    /// </summary>
    public const string ControlPlaneRead = "Krackend.ControlPlane.Read";

    /// <summary>
    /// Policy for mutating orchestration design data.
    /// </summary>
    public const string ControlPlaneDesignWrite = "Krackend.ControlPlane.Design.Write";

    /// <summary>
    /// Policy for executing release operations.
    /// </summary>
    public const string ControlPlaneReleaseExecute = "Krackend.ControlPlane.Release.Execute";

    /// <summary>
    /// Policy for product security administration.
    /// </summary>
    public const string ControlPlaneSecurityManage = "Krackend.ControlPlane.Security.Manage";

    /// <summary>
    /// Policy for reading runtime data.
    /// </summary>
    public const string RuntimeRead = "Krackend.Runtime.Read";

    /// <summary>
    /// Policy for managing runtime data.
    /// </summary>
    public const string RuntimeManage = "Krackend.Runtime.Manage";

    /// <summary>
    /// Policy for applying runtime artifacts.
    /// </summary>
    public const string RuntimeArtifactsApply = "Krackend.Runtime.Artifacts.Apply";

    /// <summary>
    /// Policy for reading runtime instances.
    /// </summary>
    public const string RuntimeInstancesRead = "Krackend.Runtime.Instances.Read";
}
