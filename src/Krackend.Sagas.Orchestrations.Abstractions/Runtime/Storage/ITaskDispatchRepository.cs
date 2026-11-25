using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists outbound task dispatch records.
/// </summary>
public interface ITaskDispatchRepository
{
    Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default);

    Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default);

    Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default);

    Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default);

    Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dispatches whose durable delivery intent was persisted but has not yet been acknowledged by its execution provider.
    /// </summary>
    /// <param name="take">Maximum number of dispatches to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending durable delivery intents.</returns>
    Task<IReadOnlyCollection<TaskDispatch>> GetQueuePending(int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Advertises storage that folds the current attempt and dispatch intent into the
/// canonical task row. Historical or exceptional attempts can still be projected,
/// while the healthy path avoids three physical rows for one logical task.
/// </summary>
public interface IEmbeddedTaskTransportStateRepository
{
    /// <summary>Gets whether current transport state is embedded in the task.</summary>
    bool EmbedsCurrentTransportState { get; }
}

/// <summary>Records a physical publication without overwriting concurrent callback or cancellation state.</summary>
public interface ITaskDispatchPublicationReceiptRepository
{
    /// <summary>Persists the first confirmed publication time independently of the saga mutation lease.</summary>
    Task RecordPublicationReceipt(Id dispatchId, DateTime publishedOnUtc, CancellationToken cancellationToken = default);
}

/// <summary>Confirms publication without contending with the independently accepted callback receipt.</summary>
public interface ITaskDispatchPublicationStateRepository
{
    /// <summary>
    /// Moves a non-terminal dispatch to its published state while preserving callback,
    /// cancellation and recovery fields owned by other concurrent paths.
    /// </summary>
    Task<bool> TryConfirmPublication(
        Id dispatchId,
        string publishedStatus,
        DateTime publishedOnUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Confirms the publication of a response-bearing command by conditionally moving
/// its dispatch, attempt and task to their waiting state in one storage transaction.
/// This graph does not require the orchestration cursor lease: a concurrent callback
/// is ordered by the affected row locks and either observes the complete waiting graph
/// or wins first and makes this confirmation a no-op.
/// </summary>
public interface ITaskDispatchAwaitedPublicationRepository
{
    /// <summary>
    /// Returns <see langword="true"/> when the waiting graph was applied and
    /// <see langword="false"/> when the dispatch was already terminal.
    /// </summary>
    Task<bool> TryConfirmAwaitedPublication(
        Id dispatchId,
        Id taskExecutionId,
        Id taskExecutionAttemptId,
        DateTime sentOnUtc,
        DateTime? responseDeadlineUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Associates a durable provider action without reverting a concurrently advanced dispatch.</summary>
public interface ITaskDispatchActionReferenceRepository
{
    /// <summary>
    /// Captures the provider action identity and promotes QueuePending to the accepted state.
    /// Any newer functional dispatch state must be preserved.
    /// </summary>
    Task CaptureActionReference(
        Id dispatchId,
        string actionId,
        string lane,
        string acceptedStatus,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the runtime entities that own one dispatch as a single consistent storage snapshot.
/// Implementations should use one provider query so the dispatch and callback hot paths do not
/// serialize several independent round trips.
/// </summary>
public interface ITaskDispatchExecutionStateReader
{
    /// <summary>Gets the instance, task, attempt and dispatch joined by the dispatch identifier.</summary>
    Task<TaskDispatchExecutionState> GetExecutionState(
        Id dispatchId,
        CancellationToken cancellationToken = default);
}

/// <summary>The complete runtime ownership graph for one task dispatch.</summary>
public sealed record TaskDispatchExecutionState(
    OrchestrationInstance Instance,
    TaskExecution Task,
    TaskExecutionAttempt Attempt,
    TaskDispatch Dispatch,
    StageExecution Stage = null);

/// <summary>
/// Rehydrates embedded attempt and dispatch projections from an already committed task row.
/// Single-replica runtimes can use this capability to carry their lease-protected state into
/// the next callback without reading the same ownership graph back from storage.
/// </summary>
public interface ITaskDispatchCachedExecutionStateReader
{
    /// <summary>Tries to rebuild the callback ownership chain from committed cached entities.</summary>
    bool TryReadCachedExecutionState(
        OrchestrationInstance instance,
        TaskExecution task,
        StageExecution stage,
        out TaskDispatchExecutionState executionState);
}

/// <summary>
/// Reads all state required to process one backchannel callback in one provider operation.
/// </summary>
/// <remarks>
/// Dispatch publication only needs <see cref="TaskDispatchExecutionState"/>. Callback processing
/// additionally needs the instance stage graph, so keeping this as a separate optional capability
/// avoids loading the graph on the outbound dispatch path.
/// </remarks>
public interface ITaskDispatchCallbackExecutionStateReader
{
    /// <summary>Gets the dispatch ownership chain and the callback execution graph.</summary>
    Task<TaskDispatchCallbackExecutionState> GetCallbackExecutionState(
        Id dispatchId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads callback state using the ownership identifiers already fenced by intake metadata.
/// This lets hot-state providers materialize the complete instance graph in one operation.
/// </summary>
public interface ITaskDispatchIdentifiedCallbackExecutionStateReader
{
    /// <summary>Gets the callback ownership chain and graph without secondary lookup round trips.</summary>
    Task<TaskDispatchCallbackExecutionState> GetCallbackExecutionState(
        Id instanceId,
        Id taskExecutionId,
        Id dispatchId,
        CancellationToken cancellationToken = default);
}

/// <summary>The complete storage snapshot required to process one callback.</summary>
public sealed record TaskDispatchCallbackExecutionState(
    TaskDispatchExecutionState ExecutionState,
    OrchestrationExecutionGraph ExecutionGraph);

/// <summary>Accepts a callback independently of the saga mutation lease.</summary>
public interface ITaskDispatchCallbackReceiptRepository
{
    /// <summary>Atomically accepts the first callback unless the dispatch is already terminal.</summary>
    Task<TaskDispatch> TryAcceptCallback(Id dispatchId, string receiptId, string payload, DateTime acceptedOnUtc,
        CancellationToken cancellationToken = default, TaskCallbackIdentity identity = null);

    /// <summary>Clears the recoverable payload after processing the matching receipt.</summary>
    Task CompleteCallback(Id dispatchId, string receiptId, CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded page of accepted callbacks requiring processing.</summary>
    Task<IReadOnlyCollection<TaskDispatch>> GetPendingCallbacks(int take, int skip, CancellationToken cancellationToken = default);
}

/// <summary>Optional identity checks required before reserving a callback receipt.</summary>
public sealed record TaskCallbackIdentity(Id? InstanceId, Id? TaskExecutionId, int AttemptNumber, Id? ArtifactId);
