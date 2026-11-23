using Krackend.Demo;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Consuming.Dispatching;
namespace Inventories.Demo;
public sealed class DemoHubConsumer : HubConsumer
{
    [Consumer("commands.demo.inventories.reserve", "1.0.0")]
    [Consumer("commands.demo.inventories.reserve-exchange", "1.0.0")]
    [Consumer("commands.demo.inventories.commit", "1.0.0")]
    [Consumer("commands.demo.inventories.release", "1.0.0")]
    [Consumer("commands.demo.inventories.restore", "1.0.0")]
    [Consumer("commands.demo.inventories.exchange", "1.0.0")]
    [Consumer("commands.demo.inventories.return", "1.0.0")]
    [Consumer("commands.demo.inventories.receipt", "1.0.0")]
    [Consumer("commands.demo.inventories.release-exchange", "1.0.0")]
    // Command execution keeps its own scoped context.
    public Task Execute(DemoCommand command, CancellationToken cancellationToken)
        => Context.Services.GetRequiredService<DemoService>().ExecuteAsync(command, cancellationToken);
}

