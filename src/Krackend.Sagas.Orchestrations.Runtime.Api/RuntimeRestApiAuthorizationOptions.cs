using Krackend.Security.AspNetCore;

namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Configures granular authorization policies for the Runtime REST API.
/// </summary>
public sealed class RuntimeRestApiAuthorizationOptions
{
    /// <summary>
    /// Gets or sets the policy required by runtime read endpoints.
    /// </summary>
    public string ReadPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy required by runtime instance diagnostics endpoints.
    /// </summary>
    public string InstancesReadPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy required by runtime management endpoints.
    /// </summary>
    public string ManagePolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy required by runtime artifact apply endpoints.
    /// </summary>
    public string ArtifactApplyPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Configures all properties with the default Krackend security policy names.
    /// </summary>
    public void UseKrackendDefaults()
    {
        ReadPolicy = KrackendAuthorizationPolicies.RuntimeRead;
        InstancesReadPolicy = KrackendAuthorizationPolicies.RuntimeInstancesRead;
        ManagePolicy = KrackendAuthorizationPolicies.RuntimeManage;
        ArtifactApplyPolicy = KrackendAuthorizationPolicies.RuntimeArtifactsApply;
    }
}
