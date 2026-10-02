using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Bootstrap;
using Krackend.Sagas.Orchestrations.RuntimeHost.Sample.Storage;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using Mule;
using Mule.EntityFrameworkCore;
using Pigeon.Messaging.Azure.ServiceBus;
using Pigeon.Messaging.Topology;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
var serviceBusConnectionString =
    builder.Configuration.GetConnectionString("AzureServiceBus") ??
    builder.Configuration["Pigeon:MessageBrokers:AzureServiceBus:ConnectionString"];
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var muleConnectionString = builder.Configuration.GetConnectionString("Mule");
var muleWorkerCount = Math.Max(1, builder.Configuration.GetValue("Mule:Runtime:WorkerCount", 256));
var muleMaxDegreeOfParallelism = Math.Max(
    1,
    builder.Configuration.GetValue("Mule:Runtime:MaxDegreeOfParallelism", muleWorkerCount));
var sqlServerStorageModelCustomizer = new RuntimeSqlServerStorageModelCustomizer();

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
    options.Theme.IconCssClass = "bi-cpu";
    options.Theme.Mode = OrchestratorWebUIThemeMode.Light;
    options.Theme.PrimaryColor = "#0b3d91";
    options.Theme.PrimaryHoverColor = "#082f6f";
    options.Theme.SidebarBackgroundColor = "#061a36";
    options.Theme.SidebarBrandBackgroundColor = "#041225";
    options.Theme.SidebarTextColor = "#eef5ff";
    options.Theme.SidebarMutedTextColor = "#9fb6d8";
    options.Theme.ContentBackgroundColor = "#f2f6fb";
    options.Theme.SurfaceColor = "#ffffff";
    options.Theme.TextColor = "#10233f";
    options.Theme.Light.MutedTextColor = "#52657f";
    options.Theme.Light.BorderColor = "#cbd8e8";
    options.Theme.Light.SubtleBackgroundColor = "#eaf1fa";
    options.Theme.Dark.PrimaryColor = "#73a8ff";
    options.Theme.Dark.PrimaryHoverColor = "#96beff";
    options.Theme.Dark.SidebarBackgroundColor = "#031021";
    options.Theme.Dark.SidebarBrandBackgroundColor = "#020a16";
    options.Theme.Dark.SidebarTextColor = "#f1f7ff";
    options.Theme.Dark.SidebarMutedTextColor = "#9eb7d9";
    options.Theme.Dark.ContentBackgroundColor = "#07111f";
    options.Theme.Dark.SurfaceColor = "#0d1b2f";
    options.Theme.Dark.TextColor = "#edf6ff";
    options.Theme.Dark.MutedTextColor = "#9fb0c8";
    options.Theme.Dark.BorderColor = "#203654";
    options.Theme.Dark.SubtleBackgroundColor = "#10243d";
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
    storage => storage.ConfigureModel = sqlServerStorageModelCustomizer.Configure);

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
            execution.MaxConcurrency = null;
            execution.QueueCapacity = null;
            execution.PrefetchCount = null;
        });
    })
    .AddRedisGossip(builder.Configuration)
    .AddMule(
        mule =>
        {
            mule.UseEntityFrameworkCore<RuntimeDbContext>();
            mule.UseFastLaneRedis(options =>
            {
                options.ConnectionString = redisConnectionString;
                options.KeyPrefix = "krackend:runtime";
                options.IntentFlushSize = 1_000;
                options.CompletionFlushSize = 2_000;
                options.FlushInterval = TimeSpan.FromMilliseconds(25);
                options.LeaseDuration = TimeSpan.FromMinutes(2);
                options.DeduplicationRetention = TimeSpan.FromDays(7);
            });
            mule.Configure(settings =>
            {
                settings.ImmediateDispatch = true;
                settings.RecoveryMode = MuleRecoveryMode.Polling;
                settings.DispatchInterval = TimeSpan.FromSeconds(1);
                settings.DispatchBatchSize = 1_000;
                settings.DispatchQueueCapacity = 0;
                settings.ExecutionQueueCapacity = 0;
                settings.WorkerCount = muleWorkerCount;
                settings.MaxDegreeOfParallelism = muleMaxDegreeOfParallelism;
                settings.MaxDrainBatchesPerCycle = int.MaxValue;
                settings.MaxDrainActionsPerCycle = 0;
                settings.DrainUntilEmpty = true;
            });
        },
        runtimeMule =>
        {
            runtimeMule.ArtifactLifecycleWorkerCount = muleWorkerCount;
            runtimeMule.ArtifactLifecycleMaxDegreeOfParallelism = muleMaxDegreeOfParallelism;
        });

builder.Services.AddKrackendOrchestrationsRuntimeButterMorph();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var migrationLock = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationLock>();
    var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
    await using var migrationLease = await migrationLock.AcquireAsync(muleConnectionString!);
    await dbContext.Database.MigrateAsync();

    if (builder.Configuration.GetValue("SeedData:HappyPath:Enabled", true))
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

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapOrchestratorRuntimeDistributionEndpoints();
app.MapOrchestratorRuntimeReactiveHub();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
