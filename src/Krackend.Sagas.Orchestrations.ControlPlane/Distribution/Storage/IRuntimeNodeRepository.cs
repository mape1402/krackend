using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

/// <summary>
/// Stores runtime nodes registered in the Control Plane.
/// </summary>
public interface IRuntimeNodeRepository
{
    /// <summary>
    /// Creates a runtime node.
    /// </summary>
    /// <param name="runtimeNode">Runtime node to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Create(RuntimeNode runtimeNode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a runtime node.
    /// </summary>
    /// <param name="runtimeNode">Runtime node to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Update(RuntimeNode runtimeNode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the runtime node lifecycle status.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="status">Next runtime node status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetStatus(Id runtimeNodeId, RuntimeNodeStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a runtime node from active configuration without breaking existing release references.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SoftDelete(Id runtimeNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one runtime node by id.
    /// </summary>
    /// <param name="runtimeNodeId">Runtime node id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime node or <c>null</c> when not found.</returns>
    Task<RuntimeNode> GetById(Id runtimeNodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one runtime node by code.
    /// </summary>
    /// <param name="code">Runtime node code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime node or <c>null</c> when not found.</returns>
    Task<RuntimeNode> GetByCode(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one runtime node by inbound client id.
    /// </summary>
    /// <param name="clientId">Inbound client id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime node or <c>null</c> when not found.</returns>
    Task<RuntimeNode> GetByInboundClientId(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged runtime nodes.
    /// </summary>
    /// <param name="pagedSettings">Paged query settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged runtime node result.</returns>
    Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}
