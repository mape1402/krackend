using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>
/// Replays a failed durable delivery for a task dispatch without creating a new task attempt.
/// </summary>
public interface ITaskDispatchReplayService
{
    /// <summary>
    /// Schedules a durable delivery replay.
    /// </summary>
    /// <param name="request">Replay request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider replay result.</returns>
    Task<TaskDispatchReplayResult> ReplayAsync(
        TaskDispatchReplayRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes a durable task delivery replay request.
/// </summary>
public sealed class TaskDispatchReplayRequest
{
    /// <summary>
    /// Gets the durable dispatch to replay.
    /// </summary>
    public required TaskDispatch Dispatch { get; init; }

    /// <summary>
    /// Gets the stable idempotency key for this replay request.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the actor that requested the replay.
    /// </summary>
    public string RequestedBy { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional operator reason.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Represents the result returned by a durable delivery replay provider.
/// </summary>
public sealed record TaskDispatchReplayResult(
    bool Succeeded,
    string Status,
    string Message,
    string ProviderActionId = "")
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TaskDispatchReplayResult Success(string status, string message, string providerActionId = "")
        => new(true, status, message, providerActionId);

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    public static TaskDispatchReplayResult Rejected(string status, string message)
        => new(false, status, message);
}

