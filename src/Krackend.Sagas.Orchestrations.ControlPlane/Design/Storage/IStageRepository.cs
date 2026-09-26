namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for stage definitions.
/// </summary>
public interface IStageRepository
{
    /// <summary>
    /// Persists a new stage definition.
    /// </summary>
    /// <param name="stageDefinition">Stage definition to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(StageDefinition stageDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing stage definition.
    /// </summary>
    /// <param name="stageDefinition">Stage definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(StageDefinition stageDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only execution condition of one stage definition.
    /// </summary>
    /// <param name="stageDefinitionId">Identifier of the stage definition.</param>
    /// <param name="executionCondition">Execution condition to persist.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetExecutionCondition(Id stageDefinitionId, ExecutionCondition executionCondition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stage definition by its identifier.
    /// </summary>
    /// <param name="stageDefinitionId">Identifier of the stage definition to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Delete(Id stageDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all stages that belong to one orchestration version.
    /// </summary>
    /// <param name="orchestrationVersionId">Parent orchestration version identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of stage definitions for the given orchestration version.</returns>
    Task<IEnumerable<StageDefinition>> GetAll(Id orchestrationVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one stage definition by its identifier.
    /// </summary>
    /// <param name="stageDefinitionId">Identifier of the stage definition.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The stage definition that matches the identifier.</returns>
    Task<StageDefinition> GetById(Id stageDefinitionId, CancellationToken cancellationToken = default);
}

