using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Creates or updates an orchestration metadata descriptor.
/// </summary>
public sealed record UpsertMetadataDescriptorCommand(
    string Id,
    string Key,
    string SourceKey,
    string DisplayName,
    string Description,
    string SchemaJson) : IRequest<string>;
