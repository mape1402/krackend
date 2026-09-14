namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines command-oriented persistence operations for trigger bindings.
/// </summary>
public interface ITriggerBindingRepository
{
    /// <summary>
    /// Persists a new trigger binding.
    /// </summary>
    /// <param name="triggerBinding">Trigger binding to create.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Create(TriggerBinding triggerBinding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing trigger binding.
    /// </summary>
    /// <param name="triggerBinding">Trigger binding with updated values.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Update(TriggerBinding triggerBinding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a trigger binding by its identifier.
    /// </summary>
    /// <param name="triggerBindingId">Identifier of the trigger binding to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Delete(Id triggerBindingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only enabled state of one trigger binding.
    /// </summary>
    /// <param name="triggerBindingId">Identifier of the trigger binding.</param>
    /// <param name="isEnabled">New enabled state.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task SetIsEnabled(Id triggerBindingId, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all trigger bindings that belong to one orchestration version.
    /// </summary>
    /// <param name="orchestrationVersionId">Parent orchestration version identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of trigger bindings for the given orchestration version.</returns>
    Task<IEnumerable<TriggerBinding>> GetAll(Id orchestrationVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one trigger binding by its identifier.
    /// </summary>
    /// <param name="triggerBindingId">Identifier of the trigger binding.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The trigger binding that matches the identifier.</returns>
    Task<TriggerBinding> GetById(Id triggerBindingId, CancellationToken cancellationToken = default);
}

