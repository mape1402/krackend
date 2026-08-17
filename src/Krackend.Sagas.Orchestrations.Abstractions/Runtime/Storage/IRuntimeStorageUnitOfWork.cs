namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Coordinates runtime storage changes inside a durable runtime action.
/// </summary>
public interface IRuntimeStorageUnitOfWork
{
    /// <summary>
    /// Gets a value indicating whether repositories should flush every mutation immediately.
    /// </summary>
    bool AutoSaveChanges { get; }

    /// <summary>
    /// Defers repository flushes until an explicit save or scope disposal.
    /// </summary>
    IDisposable DeferAutoSave();

    /// <summary>
    /// Flushes pending runtime storage changes.
    /// </summary>
    Task SaveChanges(CancellationToken cancellationToken = default);
}
