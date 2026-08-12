using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Microsoft.AspNetCore.SignalR;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

public sealed class SignalRRuntimeReactiveEventPublisher : IRuntimeReactiveEventPublisher
{
    private readonly IHubContext<RuntimeReactiveHub> _hubContext;

    public SignalRRuntimeReactiveEventPublisher(IHubContext<RuntimeReactiveHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task Publish(RuntimeReactiveEvent eventData, CancellationToken cancellationToken = default)
    {
        if (eventData is null)
            return;

        var environmentGroup = RuntimeReactiveHubGroups.Environment(eventData.EnvironmentKey);
        var instanceGroup = RuntimeReactiveHubGroups.Instance(eventData.OrchestrationInstanceId.ToString());

        await _hubContext.Clients.Groups(environmentGroup, instanceGroup)
            .SendAsync("runtime.transition", eventData, cancellationToken);
    }
}
