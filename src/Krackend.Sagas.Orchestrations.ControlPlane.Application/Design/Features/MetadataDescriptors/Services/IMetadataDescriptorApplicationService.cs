namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Provides interaction operations for orchestration metadata descriptors.
/// </summary>
public interface IMetadataDescriptorApplicationService
{
    /// <summary>
    /// Creates or updates a metadata descriptor.
    /// </summary>
    /// <param name="command">Command payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptor identifier.</returns>
    Task<string> Upsert(UpsertMetadataDescriptorCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one descriptor.
    /// </summary>
    /// <param name="command">Command payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the operation completes.</returns>
    Task<bool> Delete(DeleteMetadataDescriptorCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one descriptor by identifier.
    /// </summary>
    /// <param name="query">Query payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptor model.</returns>
    Task<MetadataDescriptorModel> GetById(GetMetadataDescriptorByIdQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paged metadata descriptors.
    /// </summary>
    /// <param name="query">Query payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged descriptor result.</returns>
    Task<ApplicationPagedResult<MetadataDescriptorModel>> GetAll(GetMetadataDescriptorsQuery query, CancellationToken cancellationToken = default);
}
