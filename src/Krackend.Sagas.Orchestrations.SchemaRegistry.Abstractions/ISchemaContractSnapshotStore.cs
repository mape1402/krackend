namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Provides optional local schema snapshots for design-time and runtime scenarios.
/// </summary>
public interface ISchemaContractSnapshotStore
{
    /// <summary>
    /// Attempts to load a schema snapshot for the supplied reference.
    /// </summary>
    Task<SchemaContractSnapshot> GetAsync(
        SchemaContractReference reference,
        CancellationToken cancellationToken = default);
}
