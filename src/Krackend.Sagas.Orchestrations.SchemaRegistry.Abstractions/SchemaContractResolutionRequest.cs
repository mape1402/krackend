namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Describes one request to resolve a schema contract snapshot.
/// </summary>
public sealed record SchemaContractResolutionRequest
{
    /// <summary>
    /// Gets the contract reference to resolve.
    /// </summary>
    public SchemaContractReference Reference { get; init; } = new();

    /// <summary>
    /// Gets whether a missing remote registry should be treated as a failure.
    /// </summary>
    public bool RequireRemoteResolution { get; init; }
}
