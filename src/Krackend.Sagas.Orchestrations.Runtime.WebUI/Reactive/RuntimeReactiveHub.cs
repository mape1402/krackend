using Microsoft.AspNetCore.SignalR;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Coordinates live runtime diagnostics subscriptions.
/// </summary>
public sealed class RuntimeReactiveHub : Hub
{
    /// <summary>
    /// Subscribes the connection to runtime node events.
    /// </summary>
    public Task WatchRuntime()
        => Groups.AddToGroupAsync(Context.ConnectionId, RuntimeReactiveHubGroups.Runtime);

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
