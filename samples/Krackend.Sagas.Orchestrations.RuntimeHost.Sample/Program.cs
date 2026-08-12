using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddKrackendSagasOrchestrationsEngine();
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
