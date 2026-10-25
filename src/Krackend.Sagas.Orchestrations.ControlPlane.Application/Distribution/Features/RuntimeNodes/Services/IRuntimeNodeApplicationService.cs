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

    /// <summary>Searches a page of runtime nodes.</summary>
    Task<ApplicationPagedResult<RuntimeNodeModel>> Search(ApplicationPagedSettings settings, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured runtime node storage does not support catalog search.");

    /// <summary>Searches enabled runtime nodes without loading disabled nodes.</summary>
    Task<ApplicationPagedResult<RuntimeNodeModel>> SearchEnabled(ApplicationPagedSettings settings, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured runtime node storage does not support enabled-node search.");
    Task<ApplicationPagedResult<RuntimeNodeModel>> SearchAllowed(ApplicationPagedSettings settings, string orchestrationId, string search, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured runtime node storage does not support allowed-node search.");

    /// <summary>Gets a runtime node directly by identifier.</summary>
    Task<RuntimeNodeModel> GetById(string runtimeNodeId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured runtime node storage does not support direct lookup.");
}
