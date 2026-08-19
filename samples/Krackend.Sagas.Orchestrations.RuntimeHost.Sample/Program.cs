using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Mule;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

builder.Services.AddKrackendSagasOrchestrationsRuntime(options =>
{
    options.EnvironmentKey = builder.Configuration["Runtime:EnvironmentKey"] ?? "local";
});

builder.Services.AddKrackendSagasOrchestrationsSqlServer(db =>
{
    db.UseSqlServer(GetRequiredConfiguration(builder.Configuration, "ConnectionStrings:Runtime"));
});

builder.Services.AddMule(mule =>
{
    mule.UseKrackendSagasOrchestrationsRuntimeStorage();
});

builder.Services.AddKrackendSagasOrchestrationsEngine();
builder.Services.AddKrackendSagasOrchestrationsRuntimeRecovery(options =>
{
    options.Enabled = true;
    options.RunOnStartup = true;
});

builder.Services.AddKrackendSagasOrchestrationsMuleDurableWork();
builder.Services.AddKrackendSagasOrchestrationsWeb(options =>
{
    options.DistributionBaseUri = builder.Configuration["Runtime:ArtifactPull:DistributionBaseUri"];
    options.RuntimeNodeId = builder.Configuration["Runtime:ArtifactPull:RuntimeNodeId"];
});
builder.Services.AddOrchestratorRuntimeWebUI(options => options.RoutePrefix = "runtime");

builder.Services.AddKrackendSagasOrchestrationsMessagingPigeon(builder.Configuration, pigeon =>
{
    pigeon.SetDomain(builder.Configuration["Pigeon:Domain"] ?? "Krackend.Sagas.Orchestrations.RuntimeHost.Sample");
    pigeon.ConfigureConsumerExecution(consumer =>
    {
        consumer.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
    });
    pigeon.SetTopologyProvisioningMode(TopologyProvisioningMode.OnStartup | TopologyProvisioningMode.OnConsume);
    pigeon.UseRabbitMq(rabbit =>
    {
        rabbit.Url = GetRequiredConfiguration(builder.Configuration, "RabbitMq:Url");
        rabbit.Exchange = GetRequiredConfiguration(builder.Configuration, "RabbitMq:Exchange");
        rabbit.ExchangeType = builder.Configuration["RabbitMq:ExchangeType"] ?? "direct";
        rabbit.DurableExchange = builder.Configuration.GetValue("RabbitMq:DurableExchange", true);
    });
});

var app = builder.Build();

app.MapStaticAssets();
app.MapGet("/", (RuntimeEnvironmentDescriptor runtime) => Results.Ok(new
{
    service = "Krackend.Sagas.Orchestrations.RuntimeHost.Sample",
    runtime.EnvironmentKey
}));

app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();
app.MapOrchestratorRuntimeReactiveHub();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.Run();

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"Configuration '{key}' is required.");

    return value;
}
