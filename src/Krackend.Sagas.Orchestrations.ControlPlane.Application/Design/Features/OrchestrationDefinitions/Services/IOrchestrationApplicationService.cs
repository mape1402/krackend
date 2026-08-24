namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines interaction operations for orchestration.
/// </summary>
public interface IOrchestrationApplicationService
{
    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    Task<string> Create(CreateOrchestrationDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Update(UpdateOrchestrationDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Activate(ActivateOrchestrationDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Deactivate(DeactivateOrchestrationDefinitionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    Task<OrchestrationDefinitionModel> GetById(GetOrchestrationDefinitionByIdQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Paged interaction result.</returns>
    Task<ApplicationPagedResult<OrchestrationDefinitionModel>> GetAll(GetOrchestrationDefinitionsQuery query, CancellationToken cancellationToken = default);
}


