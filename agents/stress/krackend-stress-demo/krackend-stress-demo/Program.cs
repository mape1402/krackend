using KrackendStressDemo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pigeon.Messaging;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
var options = BatchPublisherOptions.FromConfiguration(builder.Configuration);

builder.Services
    .AddPigeon(builder.Configuration, pigeon =>
    {
        pigeon.SetDomain(options.Domain)
            .SetTopologyProvisioningMode(
                TopologyProvisioningMode.OnStartup |
                TopologyProvisioningMode.OnPublish |
                TopologyProvisioningMode.OnConsume)
            .UseRabbitMq(rabbit =>
            {
                rabbit.Url = options.RabbitMqConnectionString;
            });
    });

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<StressMessageFactory>();
builder.Services.AddSingleton<BatchPublisher>();

using var app = builder.Build();

await app.StartAsync();
await app.Services.GetRequiredService<BatchPublisher>().PublishAsync();
await app.StopAsync();
