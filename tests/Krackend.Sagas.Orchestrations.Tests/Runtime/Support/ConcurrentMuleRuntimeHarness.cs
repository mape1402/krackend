using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Operations;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Mule;
using Id = Krackend.Sagas.Orchestrations.Abstractions.Primitives.Id;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

// Real engine, repositories, SQLite transactions and Mule dispatcher/executors.
// Only the external business service is controlled by this harness.
internal sealed class ConcurrentMuleRuntimeHarness : IAsyncDisposable
{
    public ServiceProvider Provider { get; private init; } = null!;
    public Id ArtifactId { get; private init; }
    public PublicationProbe Publications { get; private init; } = null!;
    private IReadOnlyList<IHostedService> _dispatchers = [];

    private sealed class HealthyState : IRuntimeOperationalStateProvider
    {
        public RuntimeOperationalSnapshot Current { get; } = new(RuntimeOperationalState.Healthy, DateTime.UtcNow, []);
    }

    public static async Task<ConcurrentMuleRuntimeHarness> CreateAsync(bool parallel = false, bool start = true)
    {
        Directory.CreateDirectory("artifacts");
        var database = Path.GetFullPath($"artifacts/concurrency-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var runtime = services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Singleton<IRuntimeOperationalStateProvider, HealthyState>());
        services.AddOrchestratorRuntimeStorageEntityFramework(options => options.UseSqlite($"Data Source={database};Default Timeout=3"), options =>
        {
            options.PersistDataProtectionKeysToStorage = false;
            // SQLite cannot order DateTimeOffset; all Mule timestamps are UTC.
            options.ConfigureModel = model =>
            {
                foreach (var property in model.Model.GetEntityTypes().SelectMany(x => x.GetProperties())
                    .Where(x => x.ClrType == typeof(DateTimeOffset) || x.ClrType == typeof(DateTimeOffset?)))
                    property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(value => value.UtcTicks,
                        value => new DateTimeOffset(value, TimeSpan.Zero)));
            };
        });
        var probe = new PublicationProbe();
        services.AddKeyedSingleton<IRemoteCommandExecutor>(RemoteCommandTransport.Messaging, probe);
        runtime.AddMule(mule => mule.UseEntityFrameworkCore<RuntimeDbContext>());
        services.PostConfigure<MuleSettings>(settings =>
        {
            settings.DispatchInterval = TimeSpan.FromMilliseconds(50);
            settings.CleanupMode = MuleCleanupMode.Disabled;
            settings.ImmediateDispatch = true;
            foreach (var lane in settings.Lanes.Values)
            {
                lane.WorkerCount = 1;
                lane.MaxDegreeOfParallelism = 2;
                // Production recovery is intentionally sparse. These tests exercise
                // lost local wakeups and future actions in seconds, so use a fast
                // recovery clock without changing the production default.
                lane.PollingInterval = TimeSpan.FromMilliseconds(50);
                lane.DispatchQueueCapacity = 1;
                lane.ExecutionQueueCapacity = 2;
                lane.DispatchBatchSize = 8;
            }
        });
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var artifactId = Id.New();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            var artifact = CreateArtifact(parallel);
            await scope.ServiceProvider.GetRequiredService<IRuntimeArtifactRepository>().Upsert(new RuntimeOrchestrationArtifact
            {
                Id = artifactId, OrchestrationDefinitionKey = artifact.Key, ArtifactType = "orchestration-version-snapshot",
                SourceOrchestrationVersionId = artifact.OrchestrationVersionId, Version = artifact.Version,
                ArtifactChecksum = artifact.Checksum,
                ArtifactPayload = JsonSerializer.SerializeToNode(artifact, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Status = RuntimeOrchestrationArtifactStatus.Ready, IsActive = true, IngressGeneration = 1,
                DeployedOnUtc = DateTime.UtcNow, ActivatedOnUtc = DateTime.UtcNow
            });
            await scope.ServiceProvider.GetRequiredService<IRuntimeIngressConfigurationRepository>().UpsertForArtifactAsync(artifactId,
            [new RuntimeIngressConfiguration
            {
                Id = Id.New(), RuntimeOrchestrationArtifactId = artifactId, ConfigurationKey = "backchannel:messaging",
                IngressKind = IngressKind.Backchannel, IngressTransport = IngressTransport.Messaging,
                SettingsPayload = "{\"topic\":\"test.callbacks\",\"version\":\"1.0.0\"}", IsActive = true,
                CreatedOnUtc = DateTime.UtcNow, UpdatedOnUtc = DateTime.UtcNow
            }]);
        }
        var harness = new ConcurrentMuleRuntimeHarness { Provider = provider, ArtifactId = artifactId, Publications = probe };
        probe.Runtime = harness;
        if (start) await harness.StartAsync();
        return harness;
    }

    public async Task StartAsync()
    {
        // This harness builds a bare ServiceProvider rather than an IHost, so it must
        // start the same realtime dispatchers that the production host starts.
        _dispatchers = Provider.GetServices<IHostedService>()
            .Where(service => service.GetType().Name is
                "MuleBackchannelImmediateDispatcher" or "MuleDispatcherHostedService")
            .OrderBy(service => service.GetType().Name == "MuleDispatcherHostedService")
            .ToArray();
        foreach (var dispatcher in _dispatchers)
            await dispatcher.StartAsync(CancellationToken.None);
    }

    public async Task AcceptAsync(string correlation)
    {
        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeBuffer>().EnqueueWorkAsync(new WorkItem
        {
            ArtifactId = ArtifactId.ToString(), IngressKind = IngressKind.Trigger, IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata { CorrelationId = correlation }, Payload = new JsonObject { ["value"] = correlation }
        });
    }

    public async Task CallbackAsync(RemoteCommand command)
    {
        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeBuffer>().EnqueueWorkAsync(Callback(command));
    }

    public WorkItem Callback(RemoteCommand command) => new()
    {
        ArtifactId = ArtifactId.ToString(), IngressKind = IngressKind.Backchannel, IngressTransport = IngressTransport.Messaging,
        MessageMetadata = command.MessageMetadata, ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
        { Succeeded = true, Status = "Succeeded", CompletedOnUtc = DateTime.UtcNow }, Payload = new JsonObject { ["response"] = true }
    };

    public async Task<T> ReadAsync<T>(Func<RuntimeDbContext, Task<T>> query)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<RuntimeDbContext>());
    }

    public async Task WaitForCompletedAsync(int count, TimeSpan? timeout = null)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < (timeout ?? TimeSpan.FromSeconds(15)))
        {
            if (await ReadAsync(db => db.OrchestrationInstances.CountAsync(x => x.Status == OrchestrationInstanceStatus.Completed)) == count) return;
            await Task.Delay(20);
        }
        var actions = await ReadAsync(db => db.Set<DurableAction>().AsNoTracking().ToArrayAsync());
        throw new TimeoutException(JsonSerializer.Serialize(actions.Select(x => new { Key = x.Key.ToString(), x.Status, x.Attempts, x.LastError, x.DeferralReasonCode })));
    }

    public async ValueTask DisposeAsync()
    {
        Publications.Release.TrySetResult();
        foreach (var dispatcher in _dispatchers.Reverse())
            await dispatcher.StopAsync(CancellationToken.None);
        await Provider.DisposeAsync();
    }

    private static OrchestrationArtifact CreateArtifact(bool parallel)
    {
        var group = parallel ? Id.New() : (Id?)null;
        var version = new SemanticVersion(1, 0, 0);
        var tasks = Enumerable.Range(1, parallel ? 2 : 1).Select(index => new TaskArtifact(Id.New(), $"task{index}", $"task{index}", index,
            "", TaskKind.Messaging, parallel ? TaskExecutionMode.Parallel : TaskExecutionMode.Sequential, group,
            new ExecutionConditionArtifact(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true"))),
            new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact()),
            new MessagingTaskConfigurationArtifact("test.commands", version, null!), null,
            new TimeoutPolicyArtifact(Duration.FromSeconds(60), TimeoutBehavior.Fail, new FailTimeoutBehaviorPolicyArtifact("TIMEOUT")),
            OnErrorPolicy.Stop, null, TaskDispatchType.FireAndWaitCallback, true)).ToArray();
        return new OrchestrationArtifact(Id.New(), Id.New(), "test.concurrent", "Concurrent", "tests", version,
            new Checksum("concurrency-test"), [], [],
            [new StageArtifact(Id.New(), "stage", "stage", 1, true,
                new ExecutionConditionArtifact(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true"))),
                tasks, parallel ? [new ParallelGroupArtifact(group!.Value, ParallelJoinPolicy.WaitAll, null)] : [], [])]);
    }

    internal sealed class PublicationProbe : IRemoteCommandExecutor
    {
        internal ConcurrentMuleRuntimeHarness Runtime = null!;
        internal readonly ConcurrentQueue<RemoteCommand> Commands = new();
        internal readonly TaskCompletionSource BothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool AutoCallback = true;
        internal bool Hold;
        public async Task ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command);
            if (Commands.Count >= 2) BothEntered.TrySetResult();
            if (Hold) await Release.Task.WaitAsync(cancellationToken);
            if (AutoCallback) await Runtime.CallbackAsync(command);
        }
    }
}
