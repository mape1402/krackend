using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Mule;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

internal static class MuleCooperativeExecution
{
    internal static async Task<bool> RunAsync(MuleActionContext context, Func<Task> operation)
    {
        try { await operation(); return true; }
        catch (OrchestrationInstanceLeaseUnavailableException) { Defer(context, "krackend.instance-lease-busy"); }
        catch (OrchestrationCallbackNotReadyException) { Defer(context, "krackend.callback-not-ready"); }
        catch (RuntimeStateConflictException) { Defer(context, "krackend.state-conflict"); }
        return false;
    }

    internal static void Defer(MuleActionContext context, string reason, DateTimeOffset? notBefore = null)
    {
        RuntimeConcurrencyMetrics.Deferred.Add(1, new KeyValuePair<string, object?>("reason", reason));
        context.DeferUntil(notBefore ?? DateTimeOffset.UtcNow.AddMilliseconds(Random.Shared.Next(100, 251)),
            DeferralReason.From(reason));
    }
}
