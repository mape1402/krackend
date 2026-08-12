using Microsoft.AspNetCore.SignalR;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

public sealed class RuntimeReactiveHub : Hub
{
    public Task WatchEnvironment(string environmentKey)
    {
        if (string.IsNullOrWhiteSpace(environmentKey))
            return Task.CompletedTask;

        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeReactiveHubGroups.Environment(environmentKey));
    }

    public Task WatchInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return Task.CompletedTask;

        return Groups.AddToGroupAsync(Context.ConnectionId, RuntimeReactiveHubGroups.Instance(instanceId));
    }
}
