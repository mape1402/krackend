namespace Krackend.Sagas.Orchestrations.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for task definitions.
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Persists a new task definition.
    /// </summary>
    /// <param name="taskDefinition">Task definition to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(TaskDefinition taskDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing task definition.
    /// </summary>
    /// <param name="taskDefinition">Task definition with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(TaskDefinition taskDefinition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task definition by its identifier.
    /// </summary>
    /// <param name="taskDefinitionId">Identifier of the task definition to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Delete(Id taskDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only enabled state of one task definition.
    /// </summary>
    /// <param name="taskDefinitionId">Identifier of the task definition.</param>
    /// <param name="isEnabled">New enabled state.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetIsEnabled(Id taskDefinitionId, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only execution condition of one task definition.
    /// </summary>
    /// <param name="taskDefinitionId">Identifier of the task definition.</param>
    /// <param name="executionCondition">Execution condition to persist.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetExecutionCondition(Id taskDefinitionId, ExecutionCondition executionCondition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only transformation of one task definition.
    /// </summary>
    /// <param name="taskDefinitionId">Identifier of the task definition.</param>
    /// <param name="transformation">Transformation to persist.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetTransformation(Id taskDefinitionId, TransformationDefinition transformation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all tasks that belong to one stage definition.
    /// </summary>
    /// <param name="stageDefinitionId">Parent stage definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of task definitions for the given stage definition.</returns>
    Task<IEnumerable<TaskDefinition>> GetAll(Id stageDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one task definition by its identifier.
    /// </summary>
    /// <param name="taskDefinitionId">Identifier of the task definition.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The task definition that matches the identifier.</returns>
    Task<TaskDefinition> GetById(Id taskDefinitionId, CancellationToken cancellationToken = default);
}

