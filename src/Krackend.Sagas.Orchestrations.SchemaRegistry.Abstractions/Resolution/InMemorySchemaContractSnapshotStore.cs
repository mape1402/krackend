using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;

/// <summary>
/// Stores schema snapshots in memory for tests and local development.
/// </summary>
public sealed class InMemorySchemaContractSnapshotStore : ISchemaContractSnapshotStore
{
    private readonly ConcurrentDictionary<string, SchemaContractSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds or replaces a schema snapshot.
    /// </summary>
    public void Set(SchemaContractSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshots[BuildKey(snapshot.Reference)] = snapshot;
    }

    /// <inheritdoc />
    public Task<SchemaContractSnapshot> GetAsync(
        SchemaContractReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        _snapshots.TryGetValue(BuildKey(reference), out var snapshot);
        return Task.FromResult(snapshot);
    }

    private static string BuildKey(SchemaContractReference reference)
        => string.Join(
            "|",
            reference.ProviderKey,
            reference.Kind,
            reference.ContractKey,
            reference.ContractVersion);
}
