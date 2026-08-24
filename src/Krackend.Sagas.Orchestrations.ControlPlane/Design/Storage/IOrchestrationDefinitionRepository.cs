namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for orchestration definitions.
/// </summary>
public interface IOrchestrationDefinitionRepository
{
    /// <summary>
    /// Persists a new orchestration definition.
    /// </summary>
    /// <param name="orchestrationDefinition">Definition to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(OrchestrationDefinition orchestrationDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing orchestration definition.
    /// </summary>
    /// <param name="orchestrationDefinition">Definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(OrchestrationDefinition orchestrationDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the active flag of an orchestration definition.
    /// </summary>
    /// <param name="orchestrationDefinitionId">Identifier of the orchestration definition to update.</param>
    /// <param name="isActive">New active flag value.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetIsActive(Id orchestrationDefinitionId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns orchestration definitions using paging, filtering, and sorting settings.
    /// </summary>
    /// <param name="pagedSettings">Paging, filter, and sort settings for the query.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A paged result containing orchestration definitions.</returns>
    Task<PagedResult<OrchestrationDefinition>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one orchestration definition by its identifier.
    /// </summary>
    /// <param name="orchestrationDefinitionId">Identifier of the orchestration definition.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The orchestration definition that matches the identifier.</returns>
    Task<OrchestrationDefinition> GetById(Id orchestrationDefinitionId, CancellationToken cancellationToken = default);
}

