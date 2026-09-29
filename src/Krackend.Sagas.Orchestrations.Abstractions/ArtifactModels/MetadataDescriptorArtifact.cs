namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable transversal metadata schema snapshot included in an orchestration artifact.
/// </summary>
public sealed record MetadataDescriptorArtifact(
    Id MetadataDescriptorId,
    string Key,
    string SourceKey,
    string DisplayName,
    string Description,
    string SchemaFormat,
    string SchemaJson,
    string ContentHash);
