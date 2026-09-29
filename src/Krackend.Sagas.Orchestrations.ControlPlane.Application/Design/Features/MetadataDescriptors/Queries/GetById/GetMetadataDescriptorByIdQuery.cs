using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Retrieves one orchestration metadata descriptor by identifier.
/// </summary>
public sealed record GetMetadataDescriptorByIdQuery(string MetadataDescriptorId) : IRequest<MetadataDescriptorModel>;
