namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Provides application operations for Control Plane distribution environments.
/// </summary>
public interface IDistributionEnvironmentApplicationService
{
    /// <summary>
    /// Creates or updates a distribution environment.
    /// </summary>
    /// <param name="input">Environment values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The environment id.</returns>
    Task<string> Upsert(UpsertDistributionEnvironmentInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all environments.
    /// </summary>
    /// <param name="settings">Paging settings.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A page of environments.</returns>
    Task<ApplicationPagedResult<DistributionEnvironmentModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Returns a filtered and ordered page of environments.</summary>
    Task<ApplicationPagedResult<DistributionEnvironmentModel>> Search(ApplicationPagedSettings settings, string search, string sort,
        bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured environment service does not support catalog search.");

    /// <summary>Searches enabled environments for selection.</summary>
    Task<ApplicationPagedResult<DistributionEnvironmentModel>> SearchEnabled(ApplicationPagedSettings settings, string search, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured environment service does not support enabled environment search.");
}
