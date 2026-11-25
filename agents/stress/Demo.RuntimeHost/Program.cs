using Krackend.Demo;
using Krackend.Demo.RuntimeHost;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime.HotState.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.SqlServer;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Mule;
using Mule.EntityFrameworkCore;
using Mule.FastLane.Redis;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;
using Pigeon.Messaging.Consuming.Management;
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var runtimeMinThreads = Math.Max(
    Environment.ProcessorCount * 2,
    builder.Configuration.GetValue("Demo:ThreadPoolMinThreads", 32));
ThreadPool.GetMinThreads(out var currentWorkerThreads, out var currentIoThreads);
ThreadPool.SetMinThreads(
    Math.Max(currentWorkerThreads, runtimeMinThreads),
    Math.Max(currentIoThreads, runtimeMinThreads));
var useRedisAcceleration = builder.Configuration.GetValue("Demo:UseRedisAcceleration", false);
var useRuntimeHotState = builder.Configuration.GetValue("Demo:UseRuntimeHotState", false);
var muleRedis = builder.Configuration.GetConnectionString("MuleRedis");
if ((useRedisAcceleration || useRuntimeHotState) && string.IsNullOrWhiteSpace(muleRedis))
    throw new InvalidOperationException("Set ConnectionStrings:MuleRedis.");
var fastLaneIntentFlushSize = Math.Clamp(
    builder.Configuration.GetValue("Demo:RedisAcceleration:IntentFlushSize", 1_000),
    1,
    10_000);
var fastLaneCompletionFlushSize = Math.Clamp(
    builder.Configuration.GetValue("Demo:RedisAcceleration:CompletionFlushSize", 2_000),
    1,
    20_000);
var fastLaneFlushInterval = TimeSpan.FromMilliseconds(Math.Clamp(
    builder.Configuration.GetValue("Demo:RedisAcceleration:FlushIntervalMilliseconds", 100),
    1,
    1_000));
var runtimeConnection = builder.Configuration.GetConnectionString("Demo")
    ?? throw new InvalidOperationException("Set ConnectionStrings:Demo.");
var usePostgres = string.Equals(
    builder.Configuration["Demo:RuntimeProvider"],
    "PostgreSql",
    StringComparison.OrdinalIgnoreCase);
var requireSqlServerPortableDatabaseTuning = !usePostgres && builder.Configuration.GetValue(
    "Demo:SqlServerRequirePortableDatabaseTuning",
    false);
var coordinateSqlServerDurableLogFlushes = !usePostgres && builder.Configuration.GetValue(
    "Demo:SqlServerCoordinateDurableLogFlushes",
    false);
var muleKeyPrefix = builder.Configuration["Demo:MuleKeyPrefix"] ??
    $"krackend:demo:runtime:{(usePostgres ? "postgresql" : "sqlserver")}:v3";
if (useRedisAcceleration || useRuntimeHotState)
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(muleRedis!));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<RuntimeMetricsProbe>();
builder.Services.AddOrchestratorRuntimeWebUI(options =>
{
    options.RoutePrefix = "runtime";
});
builder.Services.AddOrchestratorRuntimeStorageEntityFramework(options =>
    {
        if (usePostgres)
            options.UseNpgsql(runtimeConnection);
        else
            options.UseSqlServer(runtimeConnection, sqlServer =>
            {
                sqlServer.MigrationsAssembly(typeof(Program).Assembly.GetName().Name);
                sqlServer.MigrationsHistoryTable("__RuntimeStorageMigrationsHistory", "Runtime");
                // Runtime mutations normally touch 2-6 rows. SQL Server's EF defaults
                // avoid batching very small changes, which turns one saga transition
                // into several database round trips. Under a 200-instance burst those
                // round trips dominate SaveChanges even though the rows are unrelated.
                sqlServer.MinBatchSize(1);
                sqlServer.MaxBatchSize(100);
            });
    },
    storage =>
    {
        if (usePostgres)
            storage.ConfigureModel = new RuntimePostgresStorageModelCustomizer().Configure;
        else
            storage.UseKrackendSqlServerRuntime(sqlServer =>
            {
                sqlServer.CoordinateDurableLogFlushes = coordinateSqlServerDurableLogFlushes;
                sqlServer.DurableLogFlushWindow = TimeSpan.FromMilliseconds(Math.Clamp(
                    builder.Configuration.GetValue("Demo:SqlServerDurableLogFlushWindowMilliseconds", 8),
                    0,
                    100));
                sqlServer.DurableLogFlushConcurrency = Math.Clamp(
                    builder.Configuration.GetValue("Demo:SqlServerDurableLogFlushConcurrency", 4),
                    1,
                    64);
            });
        storage.PersistRoutineExecutionTransitions = false;
        storage.PersistSuccessfulTransportOutcomes = false;
        storage.EmbedCurrentTransportState = true;
        storage.CompressSnapshotPayload = true;
        storage.PersistStatisticsObservations = false;
        storage.HotStateProjectionInterval = TimeSpan.FromMilliseconds(750);
        storage.HotStateProjectionMaxDelay = TimeSpan.FromSeconds(15);
        storage.HotStateProjectionBatchSize = 10_000;
    });
builder.Services.AddKrackendOrchestrationsRuntime().AddPigeon(builder.Configuration, pigeon =>
{
    pigeon.SetDomain("krackend-demo");
    pigeon.SetTopologyProvisioningMode(TopologyProvisioningMode.OnStartup | TopologyProvisioningMode.OnPublish | TopologyProvisioningMode.OnConsume);
    pigeon.UseRabbitMq(rabbit => rabbit.Url = builder.Configuration["Demo:RabbitMq"] ?? "amqp://guest:guest@localhost:5672");
    pigeon.ConfigureConsumerExecution(execution => execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess);
}, pigeon => pigeon.ConfigureJsonOptions(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PropertyNameCaseInsensitive = true;
}))
    .AddMule(
        mule =>
        {
            // SQL remains the durable source in both modes. Redis is only a shared
            // front buffer that batches versioned state into the configured EF store.
            mule.UseEntityFrameworkCore<RuntimeDbContext>();
            if (useRedisAcceleration)
            {
                mule.UseFastLaneRedis(options =>
                {
                    options.ConnectionFactory = (provider, _) =>
                        ValueTask.FromResult(provider.GetRequiredService<IConnectionMultiplexer>());
                    options.DisposeConnection = false;
                    options.KeyPrefix = muleKeyPrefix;
                    // Keep projection batches below the point where one FastLane
                    // transaction monopolizes the same SQL log used by saga state.
                    options.IntentFlushSize = fastLaneIntentFlushSize;
                    options.CompletionFlushSize = fastLaneCompletionFlushSize;
                    options.FlushInterval = fastLaneFlushInterval;
                    options.LeaseDuration = TimeSpan.FromMinutes(2);
                    options.FlushClaimTimeout = TimeSpan.FromMinutes(1);
                    options.DeduplicationRetention = TimeSpan.FromDays(7);
                });
            }
        },
        runtime =>
        {
            runtime.Profile = KrackendMuleRuntimeProfile.HighThroughput;
        })
    // The task row already persists the exact deadline. Avoid creating and
    // cancelling one Mule action for every healthy awaited task; the indexed
    // deadline scan below remains the durable crash-safe timeout mechanism.
    .UseDeadlineScanningResponseTimeouts();
if (useRuntimeHotState)
{
    builder.Services.AddKrackendRuntimeHotStateRedis(options =>
    {
        options.ConnectionFactory = (provider, _) =>
            ValueTask.FromResult(provider.GetRequiredService<IConnectionMultiplexer>());
        options.DisposeConnection = false;
        options.Database = 5;
        options.KeyPrefix = builder.Configuration["Demo:RuntimeHotStateKeyPrefix"] ??
            $"krackend:demo:runtime:{(usePostgres ? "postgresql" : "sqlserver")}:hot:v1";
    });
}
builder.Services.Configure<OrchestrationTimeoutOptions>(options =>
    options.ScanIntervalSeconds = 1);
builder.Services.Configure<OrchestrationCoordinationOptions>(options =>
    options.DistributedLeaseEnabled = useRuntimeHotState);
builder.Services.Configure<MuleSettings>(settings =>
{
    settings.ImmediateDispatch = true;
    settings.RecoveryMode = MuleRecoveryMode.Scheduled;
    settings.CleanupMode = MuleCleanupMode.Disabled;
});
builder.Services.AddKrackendOrchestrationsRuntimeButterMorph();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var runtimeDb = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
    if (runtimeDb.Database.IsSqlServer() && requireSqlServerPortableDatabaseTuning)
        await ValidateSqlServerPortableDatabaseTuningAsync(runtimeDb);
    if (runtimeDb.Database.IsNpgsql())
    {
        // Npgsql's minimum pool size is a retention floor, not eager creation.
        // Materialize the physical pool before accepting broker traffic so the
        // first real burst does not pay hundreds of PostgreSQL handshakes.
        var connectionSettings = new NpgsqlConnectionStringBuilder(runtimeConnection);
        var requestedWarmConnections = Math.Max(0,
            builder.Configuration.GetValue("Demo:RuntimePoolWarmConnections", 200));
        var warmConnectionCount = Math.Min(requestedWarmConnections, connectionSettings.MaxPoolSize);
        var warmConnections = Enumerable.Range(0, warmConnectionCount)
            .Select(_ => new NpgsqlConnection(runtimeConnection))
            .ToArray();
        try
        {
            await Task.WhenAll(warmConnections.Select(connection =>
                connection.OpenAsync(CancellationToken.None)));

            // With multiplexing enabled OpenAsync creates logical connections and
            // may leave the physical pool cold. Bind every warm connection to its own
            // connector at the same time by holding an explicit transaction on all of
            // them before any is released. The first real trigger burst must not pay
            // for socket/authentication setup inside orchestration latency.
            var warmTransactions = await Task.WhenAll(warmConnections.Select(async connection =>
                await connection.BeginTransactionAsync(CancellationToken.None)));
            try
            {
                await Task.WhenAll(warmConnections.Select(async connection =>
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT 1";
                    await command.ExecuteScalarAsync(CancellationToken.None);
                }));
                await Task.WhenAll(warmTransactions.Select(transaction =>
                    transaction.CommitAsync(CancellationToken.None)));
            }
            finally
            {
                await Task.WhenAll(warmTransactions.Select(transaction =>
                    transaction.DisposeAsync().AsTask()));
            }
        }
        finally
        {
            await Task.WhenAll(warmConnections.Select(connection =>
                connection.DisposeAsync().AsTask()));
        }
    }
    else if (runtimeDb.Database.IsSqlServer())
    {
        var connectionSettings = new SqlConnectionStringBuilder(runtimeConnection);
        var requestedWarmConnections = Math.Max(0,
            builder.Configuration.GetValue("Demo:RuntimePoolWarmConnections", 200));
        var warmConnectionCount = Math.Min(requestedWarmConnections, connectionSettings.MaxPoolSize);
        var warmConnections = Enumerable.Range(0, warmConnectionCount)
            .Select(_ => new SqlConnection(runtimeConnection))
            .ToArray();
        try
        {
            await Task.WhenAll(warmConnections.Select(async connection =>
            {
                await connection.OpenAsync(CancellationToken.None);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(CancellationToken.None);
            }));
        }
        finally
        {
            await Task.WhenAll(warmConnections.Select(connection =>
                connection.DisposeAsync().AsTask()));
        }
    }
    var deployments = scope.ServiceProvider.GetRequiredService<IRuntimeArtifactDeploymentService>();
    foreach (var definition in DemoCases.All)
    {
        var artifact = DemoArtifactFactory.Create(definition);
        var deployment = await deployments.DeployAsync(new RuntimeArtifactDeliveryPackage
        {
            ReleaseTargetId = "demo:" + definition.Id,
            ArtifactId = DemoArtifactFactory.StableId("artifact:" + definition.Key + ":" + artifact.Version).ToString(),
            ArtifactType = "orchestration-version-snapshot", SchemaVersion = "1.0.0",
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId.ToString(),
            OrchestrationVersionId = artifact.OrchestrationVersionId.ToString(), OrchestrationDefinitionKey = artifact.Key,
            Version = artifact.Version.ToString(), Checksum = artifact.Checksum.Value,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(artifact, DemoJson.Options),
            CorrelationId = "demo-seed:" + definition.Id, PromotedBy = "demo-runtime", PromotedOnUtc = DateTime.UtcNow
        }, "demo-runtime", CancellationToken.None);
        if (!deployment.Accepted)
            throw new InvalidOperationException($"Demo artifact {definition.Id} was rejected: {deployment.Message}");
    }

    // A demo definition can change its trigger topology between versions (for
    // example, moving from a business-service relay to direct runtime intake).
    // Such versions do not share a trigger identity, so a normal rollout cannot
    // infer that the old ingress must be replaced. Retire completed legacy
    // versions explicitly through the runtime lifecycle API. This preserves all
    // instance/history rows while ensuring that only the current version accepts
    // new triggers or callbacks.
    var currentVersions = DemoCases.All
        .Select(DemoArtifactFactory.Create)
        .ToDictionary(x => x.Key, x => x.Version.ToString(), StringComparer.OrdinalIgnoreCase);
    var legacyArtifacts = (await runtimeDb.RuntimeOrchestrationArtifacts
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToArrayAsync())
        .Where(x => currentVersions.TryGetValue(x.OrchestrationDefinitionKey, out var currentVersion) &&
                    !string.Equals(x.Version.ToString(), currentVersion, StringComparison.Ordinal))
        .ToArray();

    foreach (var legacy in legacyArtifacts)
    {
        var lifecyclePrefix = $"demo-lifecycle:{legacy.OrchestrationDefinitionKey}:{legacy.Version}";
        var deprecation = await deployments.DeployAsync(new RuntimeArtifactDeliveryPackage
        {
            ReleaseId = lifecyclePrefix + ":deprecated",
            ReleaseTargetId = "demo:" + legacy.OrchestrationDefinitionKey,
            ArtifactId = DemoArtifactFactory.StableId(lifecyclePrefix + ":deprecated").ToString(),
            ArtifactType = "orchestration-version.deprecated",
            ReleaseStrategy = "Direct",
            SchemaVersion = "1.0.0",
            OrchestrationVersionId = legacy.SourceOrchestrationVersionId.ToString(),
            OrchestrationDefinitionKey = legacy.OrchestrationDefinitionKey,
            Version = legacy.Version.ToString(),
            Checksum = legacy.ArtifactChecksum.Value,
            PayloadJson = legacy.ArtifactPayload.ToJsonString(),
            CorrelationId = lifecyclePrefix,
            PromotedBy = "demo-runtime",
            PromotedOnUtc = DateTime.UtcNow
        }, "demo-runtime", CancellationToken.None);
        if (!deprecation.Accepted)
            throw new InvalidOperationException(
                $"Legacy artifact {legacy.OrchestrationDefinitionKey} v{legacy.Version} could not be deprecated: {deprecation.Message}");

        var archival = await deployments.DeployAsync(new RuntimeArtifactDeliveryPackage
        {
            ReleaseId = lifecyclePrefix + ":archived",
            ReleaseTargetId = "demo:" + legacy.OrchestrationDefinitionKey,
            ArtifactId = DemoArtifactFactory.StableId(lifecyclePrefix + ":archived").ToString(),
            ArtifactType = "orchestration-version.archived",
            ReleaseStrategy = "Direct",
            SchemaVersion = "1.0.0",
            OrchestrationVersionId = legacy.SourceOrchestrationVersionId.ToString(),
            OrchestrationDefinitionKey = legacy.OrchestrationDefinitionKey,
            Version = legacy.Version.ToString(),
            Checksum = legacy.ArtifactChecksum.Value,
            PayloadJson = legacy.ArtifactPayload.ToJsonString(),
            CorrelationId = lifecyclePrefix,
            PromotedBy = "demo-runtime",
            PromotedOnUtc = DateTime.UtcNow
        }, "demo-runtime", CancellationToken.None);
        if (!archival.Accepted)
            throw new InvalidOperationException(
                $"Legacy artifact {legacy.OrchestrationDefinitionKey} v{legacy.Version} could not be archived: {archival.Message}");
    }
}
app.UseRouting();
app.MapStaticAssets();
app.MapOrchestratorRuntimeReactiveHub();
app.MapRazorPages().WithStaticAssets();
app.MapGet("/health", async (Krackend.Sagas.Orchestrations.Runtime.Operations.IRuntimeOperationalStateProvider state,
    RuntimeDbContext db, CancellationToken ct) =>
    state.Current.State == Krackend.Sagas.Orchestrations.Runtime.Operations.RuntimeOperationalState.Healthy &&
    await db.RuntimeOrchestrationArtifacts.CountAsync(x => x.IsActive &&
        x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.RuntimeOrchestrationArtifactStatus.Ready, ct) == DemoCases.All.Length
        ? Results.Ok("runtime") : Results.StatusCode(503));
app.MapGet("/demo/artifacts", async (RuntimeDbContext db, CancellationToken ct) =>
    await db.RuntimeOrchestrationArtifacts.AsNoTracking().Where(x => x.IsActive &&
        x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.RuntimeOrchestrationArtifactStatus.Ready)
        .Select(x => new { Id = x.Id.ToString(), Key = x.OrchestrationDefinitionKey }).ToArrayAsync(ct));
app.MapGet("/demo/metrics", (RuntimeMetricsProbe metrics) => metrics.Snapshot());
app.MapPost("/demo/metrics/reset", (RuntimeMetricsProbe metrics) =>
{
    metrics.Reset();
    return Results.Ok();
});
app.MapGet("/demo/process", () =>
{
    ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
    ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
    ThreadPool.GetMinThreads(out var minWorkers, out var minIo);
    var gc = GC.GetGCMemoryInfo();
    return new
    {
        processors = Environment.ProcessorCount,
        threadPool = new
        {
            threads = ThreadPool.ThreadCount,
            pending = ThreadPool.PendingWorkItemCount,
            completed = ThreadPool.CompletedWorkItemCount,
            workersInUse = maxWorkers - availableWorkers,
            ioInUse = maxIo - availableIo,
            minWorkers,
            minIo,
            maxWorkers,
            maxIo
        },
        gc = new
        {
            heapBytes = GC.GetTotalMemory(false),
            committedBytes = gc.TotalCommittedBytes,
            pausePercentage = gc.PauseTimePercentage,
            gen0 = GC.CollectionCount(0),
            gen1 = GC.CollectionCount(1),
            gen2 = GC.CollectionCount(2)
        }
    };
});
app.MapGet("/demo/instances/{requestId}", async (string requestId, IRuntimeDiagnosticsReader diagnostics,
    RuntimeDbContext db, CancellationToken ct) =>
{
    var catalog = (IRuntimeInstanceCatalogReader)diagnostics;
    var page = await catalog.GetInstances(new RuntimeInstanceCatalogQuery(
        1, 10, requestId, Sort: RuntimeInstanceCatalogSort.Updated, Descending: true)
        { IncludeTotalCount = false }, ct);
    return new
    {
        instances = page.Items.Where(x => string.Equals(x.CorrelationId, requestId, StringComparison.Ordinal))
            .Select(x => new { x.Id, x.Status, x.StartedOnUtc, x.LastUpdatedOnUtc }).ToArray(),
        actions = await db.Set<DurableAction>().AsNoTracking().Where(x => x.CorrelationId == requestId)
            .Select(x => new { x.Id, x.Key, x.Status, x.Attempts, x.LastError, x.DeferralReasonCode }).ToArrayAsync(ct)
    };
});
app.MapGet("/demo/runs/{runId}/instances", async (string runId, IRuntimeDiagnosticsReader diagnostics, CancellationToken ct) =>
{
    var catalog = (IRuntimeInstanceCatalogReader)diagnostics;
    var results = new List<InstanceRowModel>();
    for (var pageNumber = 1; ; pageNumber++)
    {
        var page = await catalog.GetInstances(new RuntimeInstanceCatalogQuery(
            pageNumber, 100, runId, Sort: RuntimeInstanceCatalogSort.Started, Descending: true)
            { IncludeTotalCount = false }, ct);
        results.AddRange(page.Items.Where(x => x.CorrelationId.StartsWith(runId, StringComparison.Ordinal)));
        if (page.HasMore != true) break;
    }
    return results.DistinctBy(x => x.Id)
        .Select(x => new { x.Id, x.CorrelationId, x.Status, x.StartedOnUtc, x.LastUpdatedOnUtc }).ToArray();
});
await app.RunAsync();

static async Task ValidateSqlServerPortableDatabaseTuningAsync(RuntimeDbContext runtimeDb)
{
    var pendingMigrations = (await runtimeDb.Database.GetPendingMigrationsAsync()).ToArray();
    if (pendingMigrations.Length != 0)
    {
        throw new InvalidOperationException(
            $"The SQL Server runtime database has pending EF migrations: {string.Join(", ", pendingMigrations)}. " +
            "Generate and apply the host migration script as a DBA, then restart the runtime.");
    }

    var connection = runtimeDb.Database.GetDbConnection();
    var closeConnection = connection.State != System.Data.ConnectionState.Open;
    if (closeConnection)
        await connection.OpenAsync(CancellationToken.None);

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                [is_read_committed_snapshot_on],
                [target_recovery_time_in_seconds],
                [is_auto_update_stats_async_on]
            FROM sys.databases
            WHERE database_id = DB_ID();
            """;
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        if (!await reader.ReadAsync(CancellationToken.None))
            throw new InvalidOperationException("The SQL Server runtime database could not be inspected.");

        var rcsiEnabled = reader.GetBoolean(0);
        var targetRecoveryTimeSeconds = reader.GetInt32(1);
        var asynchronousStatisticsEnabled = reader.GetBoolean(2);
        if (!rcsiEnabled || targetRecoveryTimeSeconds != 300 || !asynchronousStatisticsEnabled)
        {
            throw new InvalidOperationException(
                "SQL Server portable database tuning is not installed. " +
                "Apply the packaged EF migration script Scripts/RuntimeStorage.sql as a DBA, then restart the runtime.");
        }
    }
    finally
    {
        if (closeConnection)
            await connection.CloseAsync();
    }
}

