namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Exposes the logical-commit and durable-barrier phases of a runtime transaction.
/// The split lets already-committed dispatch work overlap the storage flush while
/// callers still wait for durability before acknowledging their input message.
/// </summary>
public interface IRuntimePipelinedStorageTransaction : IRuntimeStorageTransaction
{
    /// <summary>
    /// Atomically commits the transaction and starts its durability barrier.
    /// </summary>
    Task CommitLogicalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until the logical commit is durable. This phase is intentionally not
    /// cancellable because durability cannot become optional after commit.
    /// </summary>
    Task AwaitDurabilityAsync();
}
