using Krackend.Demo;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Consuming.Dispatching;
namespace Email.Demo;
public sealed class DemoHubConsumer : HubConsumer
{

    [Consumer("commands.demo.email.send", "1.0.0")]
    public Task Execute(DemoCommand command, CancellationToken cancellationToken)
        => Context.Services.GetRequiredService<DemoService>().ExecuteAsync(command, cancellationToken);
}

