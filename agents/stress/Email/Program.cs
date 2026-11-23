using Krackend.Demo;
using Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;
using Pigeon.Messaging.Consuming.Management;
using System.Text.Json;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKrackendOrchestrationsClient().AddPigeon(builder.Configuration, pigeon =>
{
    pigeon.SetDomain("krackend-demo");
    pigeon.ScanConsumersFromAssemblies(typeof(Program).Assembly);
    pigeon.SetTopologyProvisioningMode(TopologyProvisioningMode.OnStartup | TopologyProvisioningMode.OnPublish | TopologyProvisioningMode.OnConsume);
    pigeon.UseRabbitMq(rabbit => rabbit.Url = builder.Configuration["Demo:RabbitMq"] ?? "amqp://guest:guest@localhost:5672");
    pigeon.ConfigureConsumerExecution(execution =>
    {
        execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
        // Activate Pigeon's handoff pump but keep handler concurrency unlimited.
        // Rabbit otherwise awaits the complete handler and serializes this queue.
        execution.MaxConcurrency = null;
        execution.QueueCapacity = int.MaxValue;
        execution.PrefetchCount = null;
    });
}, pigeon => pigeon.ConfigureJsonOptions(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PropertyNameCaseInsensitive = true;
}));
builder.Services.AddDemoApi(builder.Configuration, "email");
var app = builder.Build();
await app.InitializeDemoAsync();
app.MapDemoEndpoints("email");
app.MapGet("/health", () => Results.Ok("email"));
await app.RunAsync();

