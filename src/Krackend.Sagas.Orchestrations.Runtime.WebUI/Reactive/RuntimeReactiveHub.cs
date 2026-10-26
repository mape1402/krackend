using Microsoft.AspNetCore.SignalR;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Coordinates live runtime diagnostics subscriptions.
/// </summary>
public sealed class RuntimeReactiveHub(IRuntimeReactiveFanout fanout = null) : Hub
{
    /// <summary>
    /// Subscribes the connection to runtime node events.
    /// </summary>
    public async Task<bool> WatchRuntime()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RuntimeReactiveHubGroups.Runtime);
        return fanout is not IRuntimeReactiveFanoutHealth health || health.IsSynchronized;
    }

    /// <summary>
    /// Subscribes the connection to runtime events for one orchestration instance.
    /// </summary>
    public Task WatchInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return Task.CompletedTask;

        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeReactiveHubGroups.Instance(instanceId));
    }
}
