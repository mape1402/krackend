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
}
