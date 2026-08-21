using Microsoft.AspNetCore.SignalR;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Coordinates live runtime diagnostics subscriptions.
/// </summary>
public sealed class RuntimeReactiveHub : Hub
{
    /// <summary>
    /// Subscribes the connection to runtime events for an environment.
    /// </summary>
    public Task WatchEnvironment(string environmentKey)
    {
        if (string.IsNullOrWhiteSpace(environmentKey))
            return Task.CompletedTask;

        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeReactiveHubGroups.Environment(environmentKey));
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
