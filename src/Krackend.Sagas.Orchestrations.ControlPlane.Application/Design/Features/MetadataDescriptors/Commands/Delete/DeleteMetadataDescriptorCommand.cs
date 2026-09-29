using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Deletes an orchestration metadata descriptor.
/// </summary>
public sealed record DeleteMetadataDescriptorCommand(string MetadataDescriptorId) : IRequest<bool>;
