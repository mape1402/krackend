using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap;
using Krackend.Sagas.Orchestrations.Distribution.Interaction;

const string orchestratorRootPath = "admin";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure SQL Server (EF Core)
var sqlConnection = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(sqlConnection))
{
    throw new InvalidOperationException("SQL Server connection string is not configured. Set 'ConnectionStrings:Default' in configuration or user secrets.");
}

builder.Services.AddHealthChecks()
    .AddAsyncCheck("sql", async () =>
    {
        await using var connection = new SqlConnection(sqlConnection);
        await connection.OpenAsync();
        return HealthCheckResult.Healthy();
    }, tags: new[] { "ready" });

builder.Services.AddOrchestratorControlPlane(options =>
{
    options.AdminRootPath = orchestratorRootPath;
    options.ConfigureSqlServer = db =>
        db.UseSqlServer(sqlConnection, sqlOptions => sqlOptions.MigrationsAssembly("Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapGet("/", context =>
{
    context.Response.Redirect($"/{orchestratorRootPath}");
    return Task.CompletedTask;
});

app.MapOrchestratorArtifactDeliveryEndpoints();
app.MapRazorPages()
   .WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
