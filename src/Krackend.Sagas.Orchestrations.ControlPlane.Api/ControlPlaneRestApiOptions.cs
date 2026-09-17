namespace Krackend.Sagas.Orchestrations.ControlPlane.Api;

/// <summary>
/// Configures the optional Control Plane REST API endpoint module.
/// </summary>
public sealed class ControlPlaneRestApiOptions
{
    /// <summary>
    /// Gets or sets the route prefix where the API is mounted.
    /// </summary>
    public string RoutePrefix { get; set; } = "/api/v1/control-plane";

    /// <summary>
    /// Gets or sets an optional authorization policy required by every API endpoint.
    /// </summary>
    public string AuthorizationPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default page size used by paged endpoints.
    /// </summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>
    /// Gets or sets the maximum page size accepted by paged endpoints.
    /// </summary>
    public int MaxPageSize { get; set; } = 200;
}
