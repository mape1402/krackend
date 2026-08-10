namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines interaction operations for task.
/// </summary>
public interface ITaskInteractionService
{
    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    Task<string> Create(CreateTaskDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Update(UpdateTaskDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Delete(DeleteTaskDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Enable(EnableTaskDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Disable(DisableTaskDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only execution condition of one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> SetExecutionCondition(SetTaskExecutionConditionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only transformation of one task.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> SetTransformation(SetTaskTransformationCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection result.</returns>
    Task<IEnumerable<TaskDefinitionModel>> GetAll(GetTaskDefinitionsQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    Task<TaskDefinitionModel> GetById(GetTaskDefinitionByIdQuery query, CancellationToken cancellationToken = default);
}


