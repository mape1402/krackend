namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;

/// <summary>
/// Resolves contracts from a local schema snapshot store.
/// </summary>
public sealed class SnapshotSchemaContractResolver : ISchemaContractResolver
{
    private readonly ISchemaContractSnapshotStore _snapshotStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotSchemaContractResolver"/> class.
    /// </summary>
    public SnapshotSchemaContractResolver(ISchemaContractSnapshotStore snapshotStore)
    {
        _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    }

    /// <inheritdoc />
    public string ProviderKey => "snapshot";

    /// <inheritdoc />
    public async Task<SchemaContractResolutionResult> ResolveAsync(
        SchemaContractResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var snapshot = await _snapshotStore.GetAsync(request.Reference, cancellationToken);
        return snapshot is null
            ? SchemaContractResolutionResult.Failed(
                SchemaContractResolutionStatus.NotFound,
                $"Schema snapshot '{request.Reference.ContractKey}' v{request.Reference.ContractVersion} was not found.")
            : SchemaContractResolutionResult.Resolved(snapshot);
    }
}
