using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Requests a transport-specific cancellation for a durable task dispatch.
/// </summary>
public interface ITaskDispatchCancellationService
{
    /// <summary>
    /// Requests cancellation after the runtime has already applied its logical fence.
    /// </summary>
    /// <param name="request">Cancellation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transport cancellation result.</returns>
    Task<TaskDispatchCancellationResult> CancelAsync(
        TaskDispatchCancellationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a transport-specific durable dispatch cancellation request.
/// </summary>
public sealed class TaskDispatchCancellationRequest
{
    /// <summary>
    /// Gets the dispatch being cancelled.
    /// </summary>
    public required TaskDispatch Dispatch { get; init; }

    /// <summary>
    /// Gets the cancellation reason.
    /// </summary>
    public required string Reason { get; init; }
}

/// <summary>
/// Represents a transport-specific cancellation result.
/// </summary>
public sealed record TaskDispatchCancellationResult(bool Requested, string Status, string Message)
{
    /// <summary>
    /// Creates a result for a requested physical cancellation.
    /// </summary>
    public static TaskDispatchCancellationResult RequestedResult(string status, string message)
        => new(true, status, message);

    /// <summary>
    /// Creates a result indicating logical cancellation only.
    /// </summary>
    public static TaskDispatchCancellationResult LogicalOnly(string status, string message)
        => new(false, status, message);
}
