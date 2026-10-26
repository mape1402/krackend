namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

public sealed class SignalRStatisticsUpdatePublisherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishesLocallyAndDoesNotPropagateSignalRFailures(bool fail)
    {
        var hub = Substitute.For<IHubContext<RuntimeReactiveHub>>();
        var clients = Substitute.For<IHubClients>();
        var proxy = Substitute.For<IClientProxy>();
        hub.Clients.Returns(clients);
        clients.Group("runtime:node").Returns(proxy);
        proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(_ => fail ? Task.FromException(new InvalidOperationException("connection closed")) : Task.CompletedTask);
        var publisher = new SignalRStatisticsUpdatePublisher(hub, NullLogger<SignalRStatisticsUpdatePublisher>.Instance);

        await publisher.PublishAsync();

        await proxy.Received(1).SendCoreAsync("runtime.statistics.updated", Arg.Is<object?[]>(args => args.Length == 0), Arg.Any<CancellationToken>());
    }
}
