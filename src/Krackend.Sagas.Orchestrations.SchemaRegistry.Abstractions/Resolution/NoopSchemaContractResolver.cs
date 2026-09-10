namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;

/// <summary>
/// Resolver used when no schema registry provider has been configured.
/// </summary>
public sealed class NoopSchemaContractResolver : ISchemaContractResolver
{
    /// <inheritdoc />
    public string ProviderKey => "noop";

    /// <inheritdoc />
    public Task<SchemaContractResolutionResult> ResolveAsync(
        SchemaContractResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(SchemaContractResolutionResult.Failed(
            SchemaContractResolutionStatus.NotConfigured,
            $"No schema registry provider is configured for contract '{request.Reference.ContractKey}'."));
    }
}
