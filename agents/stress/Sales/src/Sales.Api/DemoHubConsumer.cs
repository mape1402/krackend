using Krackend.Demo;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Consuming.Dispatching;
namespace Sales.Demo;
public sealed class DemoHubConsumer : HubConsumer
{
    [Consumer("commands.demo.sales.create", "1.0.0")]
    [Consumer("commands.demo.sales.confirm", "1.0.0")]
    [Consumer("commands.demo.sales.await-payment", "1.0.0")]
    [Consumer("commands.demo.sales.claim-payment", "1.0.0")]
    [Consumer("commands.demo.sales.claim-expiration", "1.0.0")]
    [Consumer("commands.demo.sales.claim-cancellation", "1.0.0")]
    [Consumer("commands.demo.sales.cancel", "1.0.0")]
    [Consumer("commands.demo.sales.expire", "1.0.0")]
    [Consumer("commands.demo.sales.authorize-return", "1.0.0")]
    [Consumer("commands.demo.sales.authorize-exchange", "1.0.0")]
    [Consumer("commands.demo.sales.return", "1.0.0")]
    [Consumer("commands.demo.sales.exchange", "1.0.0")]
    [Consumer("commands.demo.sales.access", "1.0.0")]
    [Consumer("commands.demo.sales.communication", "1.0.0")]
    [Consumer("commands.demo.sales.receipt", "1.0.0")]
    [Consumer("commands.demo.sales.abort", "1.0.0")]
    [Consumer("commands.demo.sales.release-resolution", "1.0.0")]
    public Task Execute(DemoCommand command, CancellationToken cancellationToken)
        => Context.Services.GetRequiredService<DemoService>().ExecuteAsync(command, cancellationToken);
}

