using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

internal sealed class KnOwlContractArtifactDocument
{
    public Guid Id { get; set; }

    public ContractArtifactType ArtifactType { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid VersionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ContractKey { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string VersionNumber { get; set; } = string.Empty;

    public string Description { get; set; }

    public string PayloadSchemaJson { get; set; } = "{}";

    public string ContentHash { get; set; } = string.Empty;

    public string SourceStatus { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public ContractArtifact ToContractArtifact()
        => new()
        {
            Id = Id,
            ArtifactType = ArtifactType,
            DefinitionId = DefinitionId,
            VersionId = VersionId,
            Name = Name ?? string.Empty,
            ContractKey = GetContractKey(),
            VersionNumber = VersionNumber ?? string.Empty,
            Description = Description,
            PayloadSchemaJson = PayloadSchemaJson ?? string.Empty,
            ContentHash = ContentHash ?? string.Empty,
            SourceStatus = SourceStatus ?? string.Empty,
            CreatedAtUtc = CreatedAtUtc
        };

    private string GetContractKey()
        => string.IsNullOrWhiteSpace(ContractKey) ? Topic ?? string.Empty : ContractKey;
}
