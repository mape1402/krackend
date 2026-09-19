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
using Krackend.Sagas.Orchestrations.RuntimeHost.Mongo.Sample.Bootstrap;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using Mule;
using Mule.EntityFrameworkCore;
using Pigeon.Messaging.Topology;

var builder = WebApplication.CreateBuilder(args);
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMq");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo");
var mongoDatabaseName = builder.Configuration.GetValue("Mongo:DatabaseName", "krackend-runtime");
if (string.IsNullOrWhiteSpace(mongoConnectionString))
{
    throw new InvalidOperationException("Set ConnectionStrings:Mongo for the Runtime Mongo sample host.");
}

var muleWorkerCount = Math.Max(1, builder.Configuration.GetValue("Mule:Runtime:WorkerCount", 256));
var muleMaxDegreeOfParallelism = Math.Max(
    1,
    builder.Configuration.GetValue("Mule:Runtime:MaxDegreeOfParallelism", muleWorkerCount));

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
});
builder.Services.AddScoped<IHappyPathOrchestrationSeeder, HappyPathOrchestrationSeeder>();

builder.Services.AddOrchestratorRuntimeStorageEntityFramework(
    options =>
    {
        options.UseMongoDB(mongoConnectionString, mongoDatabaseName);
    });

builder.Services
    .AddKrackendOrchestrationsRuntime()
    .AddPigeon(builder.Configuration, pigeon =>
    {
        pigeon.SetTopologyProvisioningMode(
            TopologyProvisioningMode.OnStartup |
            TopologyProvisioningMode.OnPublish |
            TopologyProvisioningMode.OnConsume);
        pigeon.UseRabbitMq(rabbit =>
        {
            rabbit.Url = rabbitConnectionString;
        });
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
                options.KeyPrefix = builder.Configuration.GetValue(
                    "Mule:FastLane:Redis:KeyPrefix",
                    "krackend:runtime:mongo");
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
    var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

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
