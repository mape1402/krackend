namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines interaction operations for trigger binding.
/// </summary>
public interface ITriggerBindingApplicationService
{
    /// <summary>
    /// Creates a new resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Identifier or textual result of the operation.</returns>
    Task<string> Create(CreateTriggerBindingCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Update(UpdateTriggerBindingCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Delete(DeleteTriggerBindingCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables one trigger binding.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Enable(EnableTriggerBindingCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables one trigger binding.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    Task<bool> Disable(DisableTriggerBindingCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resources that match the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Collection result.</returns>
    Task<IEnumerable<TriggerBindingModel>> GetAll(GetTriggerBindingsQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one resource by identifier.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Asynchronous operation result.</returns>
    Task<TriggerBindingModel> GetById(GetTriggerBindingByIdQuery query, CancellationToken cancellationToken = default);
}

