using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKrackendSagasOrchestrationsRuntime(options =>
{
    options.EnvironmentKey = builder.Configuration["Sagas:Environment"] ?? "local";
});

builder.Services.AddKrackendSagasOrchestrationsEngine();
builder.Services.AddKrackendSagasOrchestrationsInMemoryIntakeBuffer();
builder.Services.AddKrackendSagasOrchestrationsMessaging();
builder.Services.AddKrackendSagasOrchestrationsSqlServer(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SagasRuntime"));
});
builder.Services.AddKrackendSagasOrchestrationsWeb();

var app = builder.Build();

app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();

app.Run();
