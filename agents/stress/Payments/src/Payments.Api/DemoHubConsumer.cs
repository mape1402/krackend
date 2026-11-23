using Krackend.Demo;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Consuming.Dispatching;
namespace Payments.Demo;
public sealed class DemoHubConsumer : HubConsumer
{
    [Consumer("commands.demo.payments.intent", "1.0.0")]
    [Consumer("commands.demo.payments.capture", "1.0.0")]
    [Consumer("commands.demo.payments.confirm", "1.0.0")]
    [Consumer("commands.demo.payments.cancel-intent", "1.0.0")]
    [Consumer("commands.demo.payments.refund", "1.0.0")]
    [Consumer("commands.demo.payments.refund-partial", "1.0.0")]
    [Consumer("commands.demo.payments.adjust", "1.0.0")]
    [Consumer("commands.demo.payments.receipt", "1.0.0")]
    // Command execution keeps its own scoped context.
    public Task Execute(DemoCommand command, CancellationToken cancellationToken)
        => Context.Services.GetRequiredService<DemoService>().ExecuteAsync(command, cancellationToken);
}

