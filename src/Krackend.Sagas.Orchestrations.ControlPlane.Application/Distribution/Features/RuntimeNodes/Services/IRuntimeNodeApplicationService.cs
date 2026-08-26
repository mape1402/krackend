using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Defines application operations for runtime nodes registered in the control plane.
/// </summary>
public interface IRuntimeNodeApplicationService
{
    /// <summary>
    /// Creates or updates the basic runtime node configuration.
    /// </summary>
    /// <param name="input">Runtime node values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime node id.</returns>
    Task<string> Upsert(UpsertRuntimeNodeInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a runtime node lifecycle transition.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="status">Next runtime node status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetStatus(string runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a runtime node from active configuration without breaking release references.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Delete(string runtimeNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets runtime nodes.
    /// </summary>
    /// <param name="settings">Paged query settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged runtime nodes.</returns>
    Task<ApplicationPagedResult<RuntimeNodeModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
}
