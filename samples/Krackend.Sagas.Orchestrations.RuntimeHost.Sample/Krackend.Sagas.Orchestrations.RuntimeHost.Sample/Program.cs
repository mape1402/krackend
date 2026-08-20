using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Microsoft.EntityFrameworkCore;
using Mule;
using Pigeon.Messaging.Topology;

var builder = WebApplication.CreateBuilder(args);
var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMq");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
var muleConnectionString = builder.Configuration.GetConnectionString("Mule");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<RuntimeHostMuleDbContext>(options =>
{
    options.UseSqlServer(muleConnectionString);
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
    .AddMule(mule =>
    {
        mule.UseEntityFrameworkCore<RuntimeHostMuleDbContext>();
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
            settings.RecoveryMode = MuleRecoveryMode.Scheduled;
            settings.DispatchBatchSize = 1_000;
            settings.DispatchQueueCapacity = 0;
            settings.ExecutionQueueCapacity = 0;
            settings.WorkerCount = Environment.ProcessorCount * 20;
            settings.MaxDegreeOfParallelism = 0;
            settings.MaxDrainBatchesPerCycle = int.MaxValue;
            settings.MaxDrainActionsPerCycle = 0;
            settings.DrainUntilEmpty = true;
        });
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeHostMuleDbContext>();
    await dbContext.Database.MigrateAsync();
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
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public sealed class RuntimeHostMuleDbContext : DbContext
{
    public RuntimeHostMuleDbContext(DbContextOptions<RuntimeHostMuleDbContext> options)
        : base(options)
    {
    }
}
