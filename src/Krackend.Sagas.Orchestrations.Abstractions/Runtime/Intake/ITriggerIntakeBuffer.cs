using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

/// <summary>
/// Buffers trigger intake before it is promoted to official runtime state.
/// </summary>
public interface ITriggerIntakeBuffer
{
    /// <summary>
    /// Enqueues a trigger item.
    /// </summary>
    Task<TriggerIntakeBufferResult> Enqueue(
        TriggerIntakeBufferItem item,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to lease the next item for processing.
    /// </summary>
    Task<TriggerIntakeBufferLease> TryDequeue(CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks the next item without leasing it.
    /// </summary>
    Task<TriggerIntakeBufferItem> Peek(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a leased item as completed.
    /// </summary>
    Task<TriggerIntakeBufferResult> MarkCompleted(
        Id bufferItemId,
        string leaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a leased item as failed.
    /// </summary>
    Task<TriggerIntakeBufferResult> MarkFailed(
        Id bufferItemId,
        string leaseId,
        string reason,
        CancellationToken cancellationToken = default);
}
