using Krackend.Sagas.Orchestrations.Security.AspNetCore;

namespace Krackend.Sagas.Orchestrations.Security.Api;

/// <summary>
/// Configures the optional Krackend security administration API.
/// </summary>
public sealed class KrackendSecurityApiOptions
{
    /// <summary>
    /// Gets or sets the route prefix where the security API is mounted.
    /// </summary>
    public string RoutePrefix { get; set; } = "/api/v1/control-plane/security";

    /// <summary>
    /// Gets or sets the authorization policy required by the security API.
    /// </summary>
    public string AuthorizationPolicy { get; set; } = KrackendAuthorizationPolicies.ControlPlaneSecurityManage;

    /// <summary>
    /// Gets or sets the default page size used by paged endpoints.
    /// </summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>
    /// Gets or sets the maximum page size accepted by paged endpoints.
    /// </summary>
    public int MaxPageSize { get; set; } = 200;
}
