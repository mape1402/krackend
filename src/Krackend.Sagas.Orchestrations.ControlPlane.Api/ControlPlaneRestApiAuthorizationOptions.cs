using Krackend.Security.AspNetCore;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Configures granular authorization policies for the Control Plane REST API.
/// </summary>
public sealed class ControlPlaneRestApiAuthorizationOptions
{
    /// <summary>
    /// Gets or sets the policy required by control-plane read endpoints.
    /// </summary>
    public string ReadPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy required by orchestration design write endpoints.
    /// </summary>
    public string DesignWritePolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy required by release and distribution execution endpoints.
    /// </summary>
    public string ReleaseExecutePolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy required by control-plane security management endpoints.
    /// </summary>
    public string SecurityManagePolicy { get; set; } = string.Empty;

    /// <summary>
    /// Configures all properties with the default Krackend security policy names.
    /// </summary>
    public void UseKrackendDefaults()
    {
        ReadPolicy = KrackendAuthorizationPolicies.ControlPlaneRead;
        DesignWritePolicy = KrackendAuthorizationPolicies.ControlPlaneDesignWrite;
        ReleaseExecutePolicy = KrackendAuthorizationPolicies.ControlPlaneReleaseExecute;
        SecurityManagePolicy = KrackendAuthorizationPolicies.ControlPlaneSecurityManage;
    }
}
