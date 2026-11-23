using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Mule;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

internal sealed class MuleCallbackRecoveryCursor
{
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal int Offset;
}

internal sealed class MuleCallbackRecovery(ITaskDispatchRepository dispatches, IMuleStorage storage,
    IOrchestrationDurableActionStateReader states, IMuleClient client, MuleCallbackRecoveryCursor cursor,
    ILogger<MuleCallbackRecovery> logger, IMuleBackchannelImmediateDispatcher? immediateDispatcher = null) : IOrchestrationCallbackRecovery
{
    public async Task<int> RecoverAsync(int take, CancellationToken cancellationToken = default)
    {
        if (dispatches is not ITaskDispatchCallbackReceiptRepository receipts) return 0;
        if (!await cursor.Gate.WaitAsync(0, cancellationToken)) return 0;
        try
        {
            var limit = Math.Max(1, take);
            var page = await receipts.GetPendingCallbacks(limit, cursor.Offset, cancellationToken);
            var unresolved = 0;
            var recovered = 0;
            foreach (var dispatch in page)
            {
                var completed = false;
                // Do not race a callback that this process is actively executing.
                // The marker is intentionally in memory: after a crash it disappears,
                // so startup recovery takes over immediately without a grace timer.
                if (immediateDispatcher?.IsDispatching(dispatch.Id.ToString()) == true)
                {
                    unresolved++;
                    continue;
                }
                try
                {
                    var work = JsonSerializer.Deserialize<WorkItem>(dispatch.PendingCallbackPayload)
                        ?? throw new InvalidOperationException("An accepted callback has no payload.");
                    var key = $"backchannel:{dispatch.Id}:{work.MessageMetadata?.Attempt ?? 0}";
                    var actionId = await storage.FindByDeduplicationKeyAsync(MuleActionKeys.BackchannelActionKey, key, cancellationToken);
                    if (actionId is null)
                    {
                        // This is recovery of already accepted work. Re-entering external
                        // intake would reject it while the runtime is Recovering.
                        await client.EnqueueAsync(MuleActionKeys.BackchannelActionKey, work, options =>
                        {
                            options.Lane = MuleActionKeys.BackchannelLane;
                            options.CorrelationId = work.MessageMetadata?.OrchestrationInstanceId ?? work.MessageMetadata?.CorrelationId;
                            options.DeduplicationKey = key;
                        }, cancellationToken);
                        recovered++;
                    }
                    else if (await states.ReadAsync(actionId.Value.ToString(), cancellationToken) == DurableWorkPresence.Terminal)
                    {
                        // A terminal action retains its functional retry/dead-letter outcome.
                        await receipts.CompleteCallback(dispatch.Id, dispatch.CallbackReceiptId, cancellationToken);
                        completed = true;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { logger.LogWarning(exception, "Could not recover callback receipt for dispatch '{DispatchId}'.", dispatch.Id); }
                if (!completed) unresolved++;
            }
            cursor.Offset = page.Count < limit || unresolved > int.MaxValue - cursor.Offset ? 0 : cursor.Offset + unresolved;
            return recovered;
        }
        finally { cursor.Gate.Release(); }
    }
}
