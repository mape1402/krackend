using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// Represents the result returned by a KnOwl Control Plane contract catalog call.
/// </summary>
public sealed record KnOwlContractCatalogResult
{
    /// <summary>
    /// Gets the catalog call status.
    /// </summary>
    public KnOwlContractCatalogStatus Status { get; init; }

    /// <summary>
    /// Gets the matching deployed contract artifacts.
    /// </summary>
    public IReadOnlyCollection<ContractArtifact> Contracts { get; init; } = Array.Empty<ContractArtifact>();

    /// <summary>
    /// Gets the first matching deployed contract artifact.
    /// </summary>
    public ContractArtifact Contract => Contracts.FirstOrDefault();

    /// <summary>
    /// Gets a diagnostic message for the catalog call.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful result with one contract.
    /// </summary>
    public static KnOwlContractCatalogResult Found(ContractArtifact contract)
        => FoundMany(contract is null ? Array.Empty<ContractArtifact>() : new[] { contract });

    /// <summary>
    /// Creates a successful result with many contracts.
    /// </summary>
    public static KnOwlContractCatalogResult FoundMany(IReadOnlyCollection<ContractArtifact> contracts)
        => new()
        {
            Status = KnOwlContractCatalogStatus.Found,
            Contracts = contracts ?? Array.Empty<ContractArtifact>(),
            Message = "Contract artifact resolved."
        };

    /// <summary>
    /// Creates a failed catalog result.
    /// </summary>
    public static KnOwlContractCatalogResult Failed(KnOwlContractCatalogStatus status, string message)
        => new()
        {
            Status = status,
            Message = message ?? string.Empty
        };
}
