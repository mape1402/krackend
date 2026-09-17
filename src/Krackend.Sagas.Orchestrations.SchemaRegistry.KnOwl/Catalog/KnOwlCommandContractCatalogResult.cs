using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// Represents the result returned by a KnOwl Control Plane command contract catalog call.
/// </summary>
public sealed record KnOwlCommandContractCatalogResult
{
    /// <summary>
    /// Gets the catalog call status.
    /// </summary>
    public KnOwlContractCatalogStatus Status { get; init; }

    /// <summary>
    /// Gets the deployed command request and optional reply artifacts.
    /// </summary>
    public CommandContractArtifacts<ContractArtifact> Command { get; init; }

    /// <summary>
    /// Gets a diagnostic message for the catalog call.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful command catalog result.
    /// </summary>
    public static KnOwlCommandContractCatalogResult Found(CommandContractArtifacts<ContractArtifact> command)
        => new()
        {
            Status = KnOwlContractCatalogStatus.Found,
            Command = command,
            Message = "Command contract artifacts resolved."
        };

    /// <summary>
    /// Creates a failed command catalog result.
    /// </summary>
    public static KnOwlCommandContractCatalogResult Failed(KnOwlContractCatalogStatus status, string message)
        => new()
        {
            Status = status,
            Message = message ?? string.Empty
        };
}
