using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Replays failed Krackend durable task deliveries through Mule.
/// </summary>
public sealed class MuleTaskDispatchReplayService : ITaskDispatchReplayService
{
    private const string MuleActionIdMetadataKey = "Mule.ActionId";

    private readonly IMuleReplayService _replayService;

    /// <summary>
    /// Initializes the Mule delivery replay service.
    /// </summary>
    /// <param name="replayService">Mule replay service.</param>
    public MuleTaskDispatchReplayService(IMuleReplayService replayService)
        => _replayService = replayService ?? throw new ArgumentNullException(nameof(replayService));

    /// <inheritdoc />
    public async Task<TaskDispatchReplayResult> ReplayAsync(
        TaskDispatchReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dispatch);

        if (!request.Dispatch.Metadata.TryGetValue(MuleActionIdMetadataKey, out var sourceActionIdNode) ||
            !global::Mule.Ulid.TryParse(sourceActionIdNode?.GetValue<string>(), out var sourceActionId))
        {
            return TaskDispatchReplayResult.Rejected(
                "MissingMuleActionReference",
                $"Dispatch '{request.Dispatch.Id}' does not contain a valid Mule action reference.");
        }

        if (!global::Mule.Ulid.TryParse(request.RequestId, out var requestId))
        {
            return TaskDispatchReplayResult.Rejected(
                "InvalidReplayRequest",
                $"Replay request '{request.RequestId}' is not a valid Mule identifier.");
        }

        var result = await _replayService.ReplayAsync(
            sourceActionId,
            new MuleReplayRequest
            {
                RequestId = requestId,
                RequestedBy = request.RequestedBy,
                Reason = request.Reason,
                Metadata = new Dictionary<string, string>
                {
                    ["Krackend.DispatchId"] = request.Dispatch.Id.ToString(),
                    ["Krackend.TaskExecutionAttemptId"] = request.Dispatch.TaskExecutionAttemptId.ToString()
                },
                AllowParallelReplay = false
            },
            cancellationToken);

        return result.Outcome switch
        {
            MuleReplayOutcome.Scheduled => TaskDispatchReplayResult.Success(
                result.Outcome.ToString(),
                $"Mule replay for dispatch '{request.Dispatch.Id}' was scheduled.",
                result.ReplayActionId.ToString()),
            MuleReplayOutcome.AlreadyScheduled => TaskDispatchReplayResult.Success(
                result.Outcome.ToString(),
                $"Mule replay for dispatch '{request.Dispatch.Id}' is already scheduled.",
                result.ReplayActionId.ToString()),
            _ => TaskDispatchReplayResult.Rejected(
                result.Outcome.ToString(),
                $"Mule replay for dispatch '{request.Dispatch.Id}' was rejected with outcome '{result.Outcome}'.")
        };
    }
}
