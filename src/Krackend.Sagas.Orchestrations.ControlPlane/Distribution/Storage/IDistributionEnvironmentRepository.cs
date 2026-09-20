using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

/// <summary>
/// Persists Control Plane distribution environments.
/// </summary>
public interface IDistributionEnvironmentRepository
{
    /// <summary>
    /// Creates a distribution environment.
    /// </summary>
    /// <param name="environment">Environment to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that completes when the environment has been stored.</returns>
    Task Create(DistributionEnvironment environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a distribution environment.
    /// </summary>
    /// <param name="environment">Environment to update.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that completes when the environment has been updated.</returns>
    Task Update(DistributionEnvironment environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an environment by id.
    /// </summary>
    /// <param name="environmentId">Environment id.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The matching environment.</returns>
    Task<DistributionEnvironment> GetById(Id environmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an environment by code.
    /// </summary>
    /// <param name="code">Environment code.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The matching environment, or null when it does not exist.</returns>
    Task<DistributionEnvironment> GetByCode(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of environments.
    /// </summary>
    /// <param name="pagedSettings">Paging settings.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A page of environments.</returns>
    Task<PagedResult<DistributionEnvironment>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}
