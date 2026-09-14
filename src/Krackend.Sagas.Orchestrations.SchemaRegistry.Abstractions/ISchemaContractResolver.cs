namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Resolves schema contract snapshots from one configured schema registry provider.
/// </summary>
public interface ISchemaContractResolver
{
    /// <summary>
    /// Gets the provider key handled by this resolver.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Resolves a schema contract snapshot.
    /// </summary>
    Task<SchemaContractResolutionResult> ResolveAsync(
        SchemaContractResolutionRequest request,
        CancellationToken cancellationToken = default);
}
