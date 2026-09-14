namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines interaction operations for variable.
/// </summary>
public interface IVariableApplicationService
{
    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    Task<string> Create(CreateVariableDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Update(UpdateVariableDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Delete(DeleteVariableDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection result.</returns>
    Task<IEnumerable<VariableDefinitionModel>> GetAll(GetVariableDefinitionsQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    Task<VariableDefinitionModel> GetById(GetVariableDefinitionByIdQuery query, CancellationToken cancellationToken = default);
}


