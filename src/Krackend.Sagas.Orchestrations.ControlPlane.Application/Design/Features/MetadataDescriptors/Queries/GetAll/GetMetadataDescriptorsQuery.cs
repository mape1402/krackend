using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Retrieves paged orchestration metadata descriptors.
/// </summary>
public sealed record GetMetadataDescriptorsQuery(
    ApplicationPagedSettings PagedSettings,
    string SearchText = "") : IRequest<ApplicationPagedResult<MetadataDescriptorModel>>;
