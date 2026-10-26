using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>Publishes committed statistics updates to local runtime clients.</summary>
public sealed class SignalRStatisticsUpdatePublisher(IHubContext<RuntimeReactiveHub> hub,
    ILogger<SignalRStatisticsUpdatePublisher> logger) : IRuntimeStatisticsUpdatePublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        try { await hub.Clients.Group(RuntimeReactiveHubGroups.Runtime).SendAsync("runtime.statistics.updated", cancellationToken); }
        catch (Exception exception) { logger.LogWarning(exception, "Could not notify local statistics clients."); }
    }
}
