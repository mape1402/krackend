using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>A deployed contract summary, without its schema document.</summary>
public sealed record KnOwlContractSummary
{
    /// <summary>Artifact identifier.</summary>
    public Guid Id { get; init; }
    /// <summary>Artifact kind.</summary>
    public ContractArtifactType ArtifactType { get; init; }
    /// <summary>Contract key.</summary>
    public string ContractKey { get; init; } = string.Empty;
    /// <summary>Contract version.</summary>
    public string VersionNumber { get; init; } = string.Empty;
    /// <summary>Schema content hash.</summary>
    public string ContentHash { get; init; } = string.Empty;
    /// <summary>Artifact creation time.</summary>
    public DateTime CreatedAtUtc { get; init; }
    /// <summary>Whether the artifact has a usable schema.</summary>
    public bool HasUsableSchema { get; init; }
}

/// <summary>Result of a bounded KnOwl catalog search.</summary>
public sealed record KnOwlContractSearchResult
{
    /// <summary>Call status.</summary>
    public KnOwlContractCatalogStatus Status { get; init; }
    /// <summary>Matching summaries.</summary>
    public IReadOnlyList<KnOwlContractSummary> Contracts { get; init; } = Array.Empty<KnOwlContractSummary>();
    /// <summary>Diagnostic message.</summary>
    public string Message { get; init; } = string.Empty;
}
