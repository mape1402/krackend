namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

/// <summary>
/// Defines persistence operations for orchestration metadata descriptors.
/// </summary>
public interface IMetadataDescriptorRepository
{
    /// <summary>
    /// Creates or updates a metadata descriptor.
    /// </summary>
    /// <param name="descriptor">Descriptor to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Upsert(MetadataDescriptor descriptor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a metadata descriptor.
    /// </summary>
    /// <param name="descriptorId">Descriptor identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Delete(Id descriptorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one metadata descriptor by identifier.
    /// </summary>
    /// <param name="descriptorId">Descriptor identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata descriptor.</returns>
    Task<MetadataDescriptor> GetById(Id descriptorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one metadata descriptor by key.
    /// </summary>
    /// <param name="key">Descriptor key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata descriptor or null when not found.</returns>
    Task<MetadataDescriptor> GetByKey(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all metadata descriptors.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata descriptors.</returns>
    Task<IReadOnlyCollection<MetadataDescriptor>> GetAllDescriptors(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns paged metadata descriptors optionally filtered by search text.
    /// </summary>
    /// <param name="pagedSettings">Paging settings.</param>
    /// <param name="searchText">Optional search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged result.</returns>
    Task<PagedResult<MetadataDescriptor>> GetAll(
        PagedSettings pagedSettings,
        string searchText = "",
        CancellationToken cancellationToken = default);
}
