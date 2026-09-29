using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Maps metadata descriptors to interaction models.
/// </summary>
public interface IMetadataDescriptorApplicationMapper
{
    /// <summary>
    /// Maps one metadata descriptor to an interaction model.
    /// </summary>
    /// <param name="source">Source descriptor.</param>
    /// <returns>Mapped model.</returns>
    MetadataDescriptorModel ToModel(MetadataDescriptor source);

    /// <summary>
    /// Maps paged descriptor results to interaction paged result.
    /// </summary>
    /// <param name="source">Paged source.</param>
    /// <returns>Paged interaction model result.</returns>
    ApplicationPagedResult<MetadataDescriptorModel> ToPagedModel(PagedResult<MetadataDescriptor> source);
}
