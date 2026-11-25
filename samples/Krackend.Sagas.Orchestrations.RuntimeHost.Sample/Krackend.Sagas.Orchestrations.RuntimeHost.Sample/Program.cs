using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WolfAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using Mule;
using Mule.EntityFrameworkCore;
using Pigeon.Messaging.Azure.ServiceBus;
using Pigeon.Messaging.Topology;
using WolfAuth.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
var serviceBusConnectionString =
    builder.Configuration.GetConnectionString("AzureServiceBus") ??
    builder.Configuration["Pigeon:MessageBrokers:AzureServiceBus:ConnectionString"];
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var muleConnectionString = builder.Configuration.GetConnectionString("Mule");
var fastLaneEnabled = builder.Configuration.GetValue("Runtime:Mule:FastLane:Enabled", false);
if (fastLaneEnabled && string.IsNullOrWhiteSpace(redisConnectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:Redis is required when Runtime:Mule:FastLane:Enabled is true.");
var fastLaneKeyPrefix = builder.Configuration["Runtime:Mule:FastLane:KeyPrefix"] ?? "krackend:runtime";
var fastLaneIntentFlushSize = Math.Clamp(
    builder.Configuration.GetValue("Runtime:Mule:FastLane:IntentFlushSize", 1_000),
    1,
    10_000);
var fastLaneCompletionFlushSize = Math.Clamp(
    builder.Configuration.GetValue("Runtime:Mule:FastLane:CompletionFlushSize", 2_000),
    1,
    20_000);
var fastLaneFlushInterval = TimeSpan.FromMilliseconds(Math.Clamp(
    builder.Configuration.GetValue("Runtime:Mule:FastLane:FlushIntervalMilliseconds", 100),
    1,
    1_000));
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "krackend:runtime:";
    });
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddOrchestratorRuntimeWebUI(options =>
{
    options.RoutePrefix = "runtime";
    options.Theme.Title = "Krackend";
    options.Theme.Subtitle = "Runtime";
    options.Theme.TopBarContext = "Runtime";
    options.Theme.IconCssClass = "bi-cpu";
    options.Theme.Mode = OrchestratorWebUIThemeMode.Light;
});
builder.Services.AddScoped<IHappyPathOrchestrationSeeder, HappyPathOrchestrationSeeder>();
builder.Services.AddSingleton<IDatabaseMigrationLock, SqlServerDatabaseMigrationLock>();

builder.Services.AddOrchestratorRuntimeStorageEntityFramework(
    options =>
    {
        options.UseSqlServer(muleConnectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(Program).Assembly.GetName().Name);
            sql.MigrationsHistoryTable("__RuntimeStorageMigrationsHistory", "Runtime");
        });
    },
    storage => storage.UseKrackendSqlServerRuntime());

builder.Services
    .AddKrackendOrchestrationsRuntime()
    .AddPigeon(builder.Configuration, pigeon =>
    {
        pigeon.SetTopologyProvisioningMode(TopologyProvisioningMode.Manual);
        if (!string.IsNullOrWhiteSpace(serviceBusConnectionString))
        {
            pigeon.UseAzureServiceBus(serviceBus =>
            {
                serviceBus.ConnectionString = serviceBusConnectionString;
            });
        }

        pigeon.ConfigureConsumerExecution(execution =>
        {
            builder.Configuration.GetSection("Pigeon:ConsumerExecution").Bind(execution);
        });
    })
    .AddRedisGossip(builder.Configuration)
    .AddMule(
        mule =>
        {
            mule.UseEntityFrameworkCore<RuntimeDbContext>();
            if (fastLaneEnabled)
            {
                mule.UseFastLaneRedis(options =>
                {
                    options.ConnectionString = redisConnectionString;
                    options.KeyPrefix = fastLaneKeyPrefix;
                    options.IntentFlushSize = fastLaneIntentFlushSize;
                    options.CompletionFlushSize = fastLaneCompletionFlushSize;
                    options.FlushInterval = fastLaneFlushInterval;
                    options.LeaseDuration = TimeSpan.FromMinutes(2);
                    options.DeduplicationRetention = TimeSpan.FromDays(7);
                });
            }
            mule.Configure(settings =>
            {
                settings.ImmediateDispatch = true;
            });
        },
        runtimeMule => builder.Configuration.GetSection("Runtime:Mule").Bind(runtimeMule));

builder.Services.AddKrackendOrchestrationsRuntimeButterMorph();

var app = builder.Build();

var applyMigrationsOnStartup = builder.Configuration.GetValue(
    "Runtime:Storage:ApplyMigrationsOnStartup",
    false);
var seedHappyPath = builder.Configuration.GetValue("SeedData:HappyPath:Enabled", false);
if (applyMigrationsOnStartup || seedHappyPath)
{
    using var scope = app.Services.CreateScope();
    var migrationLock = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationLock>();
    var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
    if (applyMigrationsOnStartup)
    {
        await using var migrationLease = await migrationLock.AcquireAsync(muleConnectionString!);
        await dbContext.Database.MigrateAsync();
    }

    if (seedHappyPath)
    {
        var happyPathSeeder = scope.ServiceProvider.GetRequiredService<IHappyPathOrchestrationSeeder>();
        await happyPathSeeder.SeedAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseOrchestratorWebUIStatusPages();
app.UseHttpsRedirection();

app.UseRouting();

if (app.Services.GetService<IAuthenticationSchemeProvider>() is not null)
{
    app.UseAuthentication();
}

if (app.Services.GetService<IKrackendWolfAuthRegistration>() is not null)
{
    app.UseWolfAuth();
}

app.UseKrackendWolfAuthLoginGate();
app.UseAuthorization();

app.MapStaticAssets();
app.MapOrchestratorRuntimeDistributionEndpoints();
app.MapOrchestratorRuntimeReactiveHub();
app.MapKrackendWolfAuthEndpoints();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
