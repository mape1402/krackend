using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Mule;

var builder = WebApplication.CreateBuilder(args);

var environmentKey = builder.Configuration["Runtime:EnvironmentKey"];
var runtimeConnection = builder.Configuration.GetConnectionString("Runtime");

if (string.IsNullOrWhiteSpace(runtimeConnection))
{
    throw new InvalidOperationException("Runtime SQL Server connection string is not configured. Set 'ConnectionStrings:Runtime'.");
}

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.AddKrackendSagasOrchestrationsRuntime(options =>
{
    options.EnvironmentKey = string.IsNullOrWhiteSpace(environmentKey)
        ? "local"
        : environmentKey;
});
builder.Services.AddKrackendSagasOrchestrationsInMemoryIntakeBuffer(options =>
{
    options.Capacity = builder.Configuration.GetValue("Runtime:Intake:InMemory:Capacity", 10_000);
});
builder.Services.AddKrackendSagasOrchestrationsWeb(options =>
{
    options.DistributionBaseUri = builder.Configuration["Runtime:ArtifactPull:DistributionBaseUri"];
    options.RuntimeNodeId = builder.Configuration["Runtime:ArtifactPull:RuntimeNodeId"];
});
builder.Services.AddKrackendSagasOrchestrationsSqlServer(db =>
    db.UseSqlServer(
        runtimeConnection,
        sqlOptions => sqlOptions.MigrationsAssembly("Krackend.Sagas.Orchestrations.RuntimeHost.Sample")));
builder.Services.AddMule(mule =>
{
    mule.UseKrackendSagasOrchestrationsRuntimeStorage();
    mule.Configure(settings =>
    {
        settings.WorkerCount = builder.Configuration.GetValue("Mule:WorkerCount", 2);
        settings.MaxDegreeOfParallelism = builder.Configuration.GetValue("Mule:MaxDegreeOfParallelism", 16);
        settings.DispatchBatchSize = builder.Configuration.GetValue("Mule:DispatchBatchSize", 100);
        settings.DispatchQueueCapacity = builder.Configuration.GetValue("Mule:DispatchQueueCapacity", 2_000);

        ConfigureLane(settings, RuntimeDurableWorkLanes.ResponseIngress, priority: 100, workers: 2, parallelism: 16, batchSize: 100, queueCapacity: 2_000);
        ConfigureLane(settings, RuntimeDurableWorkLanes.TriggerIngress, priority: 90, workers: 2, parallelism: 16, batchSize: 100, queueCapacity: 2_000);
        ConfigureLane(settings, RuntimeDurableWorkLanes.Dispatch, priority: 80, workers: 2, parallelism: 16, batchSize: 100, queueCapacity: 2_000);
        ConfigureLane(settings, RuntimeDurableWorkLanes.Compensation, priority: 70, workers: 1, parallelism: 8, batchSize: 50, queueCapacity: 1_000);
        ConfigureLane(settings, RuntimeDurableWorkLanes.Reconcile, priority: 10, workers: 1, parallelism: 4, batchSize: 25, queueCapacity: 500);
    });
});
builder.Services.AddKrackendSagasOrchestrationsEngine();
builder.Services.AddKrackendSagasOrchestrationsMuleDurableWork();
builder.Services.AddOrchestratorRuntimeWebUI(options =>
{
    options.RoutePrefix = "runtime";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapStaticAssets();

app.MapGet("/", (RuntimeEnvironmentDescriptor runtime) => Results.Ok(new
{
    service = "Krackend.Sagas.Orchestrations.RuntimeHost.Sample",
    runtime.EnvironmentKey
}));

app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();
app.MapOrchestratorRuntimeReactiveHub();
app.MapRazorPages()
   .WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

app.Run();

static void ConfigureLane(
    MuleSettings settings,
    string lane,
    int priority,
    int workers,
    int parallelism,
    int batchSize,
    int queueCapacity)
{
    settings.Lanes[lane] = new MuleLaneSettings
    {
        Priority = priority,
        WorkerCount = workers,
        MaxDegreeOfParallelism = parallelism,
        DispatchBatchSize = batchSize,
        DispatchQueueCapacity = queueCapacity,
        PollingInterval = TimeSpan.FromMilliseconds(500)
    };
}
