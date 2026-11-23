using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Cancels pending Krackend task delivery work through Mule's durable cancellation API.
/// </summary>
public sealed class MuleTaskDispatchCancellationService : ITaskDispatchCancellationService
{
    private const string MuleActionIdMetadataKey = "Mule.ActionId";
    private const string ReplayActionIdMetadataKey = "Recovery.DispatchReplayProviderActionId";
    private const string ReplayRequestIdMetadataKey = "Recovery.DispatchReplayRequestId";
    private const string DeliveryCommandMetadataKey = "Runtime.Delivery.Command";
    private const string CancellationSourceStatusMetadataKey = "Recovery.DispatchCancellationSourceStatus";
    private const int MissingActionTerminalAttemptCount = 12;

    private readonly IMuleCancellationService _cancellationService;
    private readonly Func<IMuleReplayService> _replayService;
    private readonly IMuleStorage _storage;

    /// <summary>
    /// Initializes the Mule task dispatch cancellation service.
    /// </summary>
    public MuleTaskDispatchCancellationService(
        IMuleCancellationService cancellationService,
        IMuleReplayService replayService,
        IMuleStorage storage)
        : this(cancellationService, () => replayService, storage)
    {
        ArgumentNullException.ThrowIfNull(replayService);
    }

    internal MuleTaskDispatchCancellationService(
        IMuleCancellationService cancellationService,
        Func<IMuleReplayService> replayService,
        IMuleStorage storage)
    {
        _cancellationService = cancellationService ?? throw new ArgumentNullException(nameof(cancellationService));
        _replayService = replayService ?? throw new ArgumentNullException(nameof(replayService));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <inheritdoc />
    public async Task<TaskDispatchCancellationResult> CancelAsync(
        TaskDispatchCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Dispatch);

        var actionId = await ResolveActionIdAsync(request.Dispatch, cancellationToken);
        if (actionId is null)
        {
            // The healthy realtime path publishes directly and deliberately never
            // creates a Mule action. Its persisted command snapshot is still the
            // recovery intent, but there is no physical Mule work to cancel after
            // publication. Treat that absence as terminal instead of polling Mule
            // forever for an action id that cannot exist. If direct publication
            // failed and recovery materialized an action, ResolveActionIdAsync
            // above finds that action and the normal cancellation path still wins.
            if (IsPreparedDirectDispatch(request.Dispatch))
            {
                return TaskDispatchCancellationResult.LogicalOnly(
                    "DirectDispatchLogicalFence",
                    $"Dispatch '{request.Dispatch.Id}' used direct publication; no durable Mule action remains to cancel.");
            }

            // Legacy dispatches predate the direct-publication marker. Once Mule
            // storage has repeatedly confirmed that no action exists, continuing
            // every few seconds can never improve the fence and only creates an
            // unbounded recovery/write loop. Replay reservations are excluded:
            // their provider action can legitimately be materialized later.
            if (!IsReplayPending(request.Dispatch) &&
                request.Dispatch.PhysicalCancellationAttemptCount >= MissingActionTerminalAttemptCount)
            {
                return TaskDispatchCancellationResult.LogicalOnly(
                    "ActionReferenceUnavailable",
                    $"No durable Mule action exists for dispatch '{request.Dispatch.Id}' after repeated reconciliation; the logical fence remains active.");
            }

            return TaskDispatchCancellationResult.Deferred(
                "ActionReferencePending",
                $"Mule action reference for dispatch '{request.Dispatch.Id}' is not durable yet.");
        }

        var result = await _cancellationService.CancelAsync(actionId.Value, cancellationToken);
        return result.Outcome switch
        {
            MuleCancellationOutcome.Cancelled => TaskDispatchCancellationResult.RequestedResult(
                result.Outcome.ToString(),
                $"Mule action '{actionId}' was cancelled."),
            MuleCancellationOutcome.AlreadyCancelled => TaskDispatchCancellationResult.FinalResult(
                result.Outcome.ToString(),
                $"Mule action '{actionId}' was already cancelled."),
            MuleCancellationOutcome.TooLate => TaskDispatchCancellationResult.FinalResult(
                result.Outcome.ToString(),
                $"Mule action '{actionId}' was already claimed or reached a terminal state; Krackend's dispatch fence remains active."),
            MuleCancellationOutcome.NotFound => TaskDispatchCancellationResult.FinalResult(
                result.Outcome.ToString(),
                $"Mule action '{actionId}' no longer exists."),
            MuleCancellationOutcome.NotSupported => TaskDispatchCancellationResult.LogicalOnly(
                result.Outcome.ToString(),
                "The configured Mule storage provider does not support durable cancellation."),
            _ => TaskDispatchCancellationResult.Deferred(
                result.Outcome.ToString(),
                $"Mule cancellation for action '{actionId}' has not reached a terminal outcome.")
        };
    }

    private async Task<global::Mule.Ulid?> ResolveActionIdAsync(
        TaskDispatch dispatch,
        CancellationToken cancellationToken)
    {
        if (TryReadUlid(dispatch.Metadata, ReplayActionIdMetadataKey, out var replayActionId))
        {
            return replayActionId;
        }

        if (TryReadUlid(dispatch.Metadata, ReplayRequestIdMetadataKey, out var replayRequestId))
        {
            var replay = await _replayService().GetReplayAsync(replayRequestId, cancellationToken);
            if (replay is not null)
            {
                dispatch.Metadata[ReplayActionIdMetadataKey] = JsonValue.Create(replay.ReplayActionId.ToString());
                return replay.ReplayActionId;
            }

            return null;
        }

        if (dispatch.Metadata.TryGetValue("Recovery.DispatchCancellationSourceStatus", out var sourceStatus) &&
            string.Equals(sourceStatus?.ToString(), "ReplayPending", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (TryReadUlid(dispatch.Metadata, MuleActionIdMetadataKey, out var actionId))
        {
            return actionId;
        }

        var storedActionId = await _storage.FindByDeduplicationKeyAsync(
            MuleActionKeys.RemoteCommandDispatchActionKey,
            dispatch.Id.ToString(),
            cancellationToken);
        if (storedActionId is null)
        {
            return null;
        }

        dispatch.Metadata[MuleActionIdMetadataKey] = JsonValue.Create(storedActionId.Value.ToString());
        return storedActionId;
    }

    private static bool IsPreparedDirectDispatch(TaskDispatch dispatch)
    {
        if (!dispatch.Metadata.TryGetValue(DeliveryCommandMetadataKey, out var command) || command is not JsonObject commandObject)
        {
            return false;
        }

        return TryReadBoolean(commandObject, "isPreparedDirectActivation") ||
               TryReadBoolean(commandObject, "IsPreparedDirectActivation");
    }

    private static bool IsReplayPending(TaskDispatch dispatch)
        => dispatch.Metadata.TryGetValue(CancellationSourceStatusMetadataKey, out var sourceStatus) &&
           string.Equals(sourceStatus?.ToString(), "ReplayPending", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadBoolean(JsonObject value, string propertyName)
    {
        if (!value.TryGetPropertyValue(propertyName, out var property) || property is null)
        {
            return false;
        }

        try
        {
            return property.GetValue<bool>();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryReadUlid(
        IReadOnlyDictionary<string, JsonNode> metadata,
        string key,
        out global::Mule.Ulid value)
    {
        value = default;
        if (!metadata.TryGetValue(key, out var node) || node is null)
        {
            return false;
        }

        try
        {
            return global::Mule.Ulid.TryParse(node.GetValue<string>(), out value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
