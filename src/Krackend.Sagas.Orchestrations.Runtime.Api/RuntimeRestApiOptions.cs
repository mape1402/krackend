namespace Krackend.Sagas.Orchestrations.Runtime.Api;

/// <summary>
/// Configures the optional Runtime REST API endpoint module.
/// </summary>
public sealed class RuntimeRestApiOptions
{
    /// <summary>
    /// Gets or sets the route prefix where the API is mounted.
    /// </summary>
    public string RoutePrefix { get; set; } = "/api/v1/runtime";

    /// <summary>
    /// Gets or sets an optional authorization policy required by every API endpoint.
    /// </summary>
    public string AuthorizationPolicy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default page size used by list endpoints.
    /// </summary>
    public int DefaultPageSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum page size accepted by list endpoints.
    /// </summary>
    public int MaxPageSize { get; set; } = 500;
}
