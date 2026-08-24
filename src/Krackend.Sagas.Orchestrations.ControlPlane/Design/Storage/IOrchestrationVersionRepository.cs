namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for orchestration versions.
/// </summary>
public interface IOrchestrationVersionRepository
{
    /// <summary>
    /// Persists a new orchestration version.
    /// </summary>
    /// <param name="orchestrationVersion">Version to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(OrchestrationVersion orchestrationVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing orchestration version.
    /// </summary>
    /// <param name="orchestrationVersion">Version with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(OrchestrationVersion orchestrationVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the lifecycle status of an orchestration version.
    /// </summary>
    /// <param name="orchestrationVersionId">Identifier of the orchestration version to update.</param>
    /// <param name="status">New lifecycle status value.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetStatus(Id orchestrationVersionId, OrchestrationVersionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns versions for one orchestration definition using paging, filtering, and sorting settings.
    /// </summary>
    /// <param name="orchestrationDefinitionId">Parent orchestration definition identifier.</param>
    /// <param name="pagedSettings">Paging, filter, and sort settings for the query.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A paged result containing orchestration versions.</returns>
    Task<PagedResult<OrchestrationVersion>> GetAll(Id orchestrationDefinitionId, PagedSettings pagedSettings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one orchestration version by its identifier.
    /// </summary>
    /// <param name="orchestrationVersionId">Identifier of the orchestration version.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The orchestration version that matches the identifier.</returns>
    Task<OrchestrationVersion> GetById(Id orchestrationVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest orchestration version for one orchestration definition.
    /// </summary>
    /// <param name="orchestrationDefinitionId">Parent orchestration definition identifier.</param>
    /// <param name="excludeOrchestrationVersionId">Optional version identifier to exclude from the lookup.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The latest orchestration version if one exists; otherwise null.</returns>
    Task<OrchestrationVersion> GetLatestByOrchestrationDefinitionId(
        Id orchestrationDefinitionId,
        Id? excludeOrchestrationVersionId = null,
        CancellationToken cancellationToken = default);
}

