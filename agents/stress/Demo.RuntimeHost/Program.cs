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
var useSqlServerMemoryOptimizedExecutionState = builder.Configuration.GetValue(
    "Demo:SqlServerMemoryOptimizedExecutionState",
    false);
var applySqlServerDevelopmentTuning = !usePostgres && builder.Configuration.GetValue(
    "Demo:SqlServerApplyDevelopmentTuning",
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
        storage.ConfigureModel = usePostgres
            ? new RuntimePostgresStorageModelCustomizer().Configure
            : new RuntimeSqlServerStorageModelCustomizer(
                useSqlServerMemoryOptimizedExecutionState).Configure;
        storage.PersistRoutineExecutionTransitions = false;
        storage.PersistSuccessfulTransportOutcomes = false;
        storage.EmbedCurrentTransportState = true;
        storage.CompressSnapshotPayload = true;
        storage.PersistStatisticsObservations = false;
        storage.HotStateProjectionInterval = TimeSpan.FromMilliseconds(750);
        storage.HotStateProjectionMaxDelay = TimeSpan.FromSeconds(15);
        storage.HotStateProjectionBatchSize = 10_000;
        storage.CoordinateSqlServerDurableLogFlushes = coordinateSqlServerDurableLogFlushes;
        storage.SqlServerDurableLogFlushWindow = TimeSpan.FromMilliseconds(Math.Clamp(
            builder.Configuration.GetValue("Demo:SqlServerDurableLogFlushWindowMilliseconds", 8),
            0,
            100));
        storage.SqlServerDurableLogFlushConcurrency = Math.Clamp(
            builder.Configuration.GetValue("Demo:SqlServerDurableLogFlushConcurrency", 4),
            1,
            64);
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
    if (!usePostgres && useSqlServerMemoryOptimizedExecutionState)
        await EnsureSqlServerMemoryOptimizedDatabaseAsync(runtimeConnection);
    var runtimeDb = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();
    await runtimeDb.Database.EnsureCreatedAsync();
    if (runtimeDb.Database.IsSqlServer() && applySqlServerDevelopmentTuning)
    {
    // This runtime is an OLTP workload: diagnostics and status polling must never
    // take shared locks that stall unrelated saga commits. Row-versioned READ
    // COMMITTED preserves transaction semantics while allowing readers and writers
    // to progress concurrently.
    await runtimeDb.Database.ExecuteSqlRawAsync("""
        IF EXISTS (
            SELECT 1
            FROM sys.databases
            WHERE database_id = DB_ID()
              AND is_read_committed_snapshot_on = 0)
        BEGIN
            DECLARE @runtimeDatabaseName nvarchar(258) = QUOTENAME(DB_NAME());
            EXEC(N'ALTER DATABASE ' + @runtimeDatabaseName +
                N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;');
        END;
        IF EXISTS (
            SELECT 1
            FROM sys.databases
            WHERE database_id = DB_ID()
              AND recovery_model_desc <> N'SIMPLE')
        BEGIN
            DECLARE @simpleRuntimeDatabaseName nvarchar(258) = QUOTENAME(DB_NAME());
            EXEC(N'ALTER DATABASE ' + @simpleRuntimeDatabaseName + N' SET RECOVERY SIMPLE;');
        END;
        DECLARE @checkpointRuntimeDatabaseName nvarchar(258) = QUOTENAME(DB_NAME());
        EXEC(N'ALTER DATABASE ' + @checkpointRuntimeDatabaseName +
            N' SET TARGET_RECOVERY_TIME = 300 SECONDS;');
        EXEC(N'ALTER DATABASE ' + @checkpointRuntimeDatabaseName +
            N' SET AUTO_UPDATE_STATISTICS_ASYNC ON;');
        """);
    await runtimeDb.Database.ExecuteSqlRawAsync(coordinateSqlServerDurableLogFlushes
        ? "DECLARE @name nvarchar(258) = QUOTENAME(DB_NAME()); EXEC(N'ALTER DATABASE ' + @name + N' SET DELAYED_DURABILITY = FORCED;');"
        : "DECLARE @name nvarchar(258) = QUOTENAME(DB_NAME()); EXEC(N'ALTER DATABASE ' + @name + N' SET DELAYED_DURABILITY = DISABLED;');");
    // Timestamp-leading runtime IDs preserve insertion locality. Cluster on that
    // identifier directly: hashing it caused near one page split per insert.
    await runtimeDb.Database.ExecuteSqlRawAsync("""
        SET QUOTED_IDENTIFIER ON;
        SET ANSI_NULLS ON;
        SET ANSI_PADDING ON;
        SET ANSI_WARNINGS ON;
        SET ARITHABORT ON;
        SET CONCAT_NULL_YIELDS_NULL ON;
        SET NUMERIC_ROUNDABORT OFF;

        -- The EF model declares this key unique, but EnsureCreated does not repair an
        -- older non-unique index. Mule retries rely on this physical constraint as the
        -- atomic winner when two deliveries race to create the same saga.
        IF OBJECTPROPERTYEX(OBJECT_ID(N'[Runtime].[OrchestrationInstances]'), N'TableIsMemoryOptimized') = 1
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM [Runtime].[OrchestrationInstances]
                GROUP BY [StartIdempotencyKey]
                HAVING COUNT_BIG(*) > 1)
                THROW 51001, 'Duplicate orchestration start idempotency keys must be repaired before startup.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND is_unique = 1
                  AND name IN (
                      N'IX_OrchestrationInstances_StartIdempotencyKey',
                      N'UX_OrchestrationInstances_StartIdempotencyKey'))
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    ADD INDEX [UX_OrchestrationInstances_StartIdempotencyKey]
                    UNIQUE NONCLUSTERED ([StartIdempotencyKey]);');

            IF EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_StartIdempotencyKey'
                  AND is_unique = 0)
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    DROP INDEX [IX_OrchestrationInstances_StartIdempotencyKey];');

            -- These were filtered indexes in the portable model. Hekaton strips the
            -- filters, turning them into a hot full-table revision index and a second
            -- copy of the primary-key shape. Neither serves its intended predicate.
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_StatisticsRevision')
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    DROP INDEX [IX_OrchestrationInstances_StatisticsRevision];');

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_DispatchCancellationPending_Id')
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    DROP INDEX [IX_OrchestrationInstances_DispatchCancellationPending_Id];');

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_Running_StartedOnUtc_Id')
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    DROP INDEX [IX_OrchestrationInstances_Running_StartedOnUtc_Id];');

            -- A memory-optimized nonclustered index supports only its declared
            -- traversal direction. Without this DESC index, TOP (25) newest rows
            -- scans and sorts the complete instance history for every grid block.
            IF EXISTS (
                SELECT 1
                FROM sys.indexes AS i
                JOIN sys.index_columns AS ic
                  ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND i.name = N'IX_OrchestrationInstances_StartedOnUtc_Id'
                  AND ic.key_ordinal > 0
                  AND ic.is_descending_key = 0)
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    DROP INDEX [IX_OrchestrationInstances_StartedOnUtc_Id];');

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_StartedOnUtc_Id')
                EXEC(N'ALTER TABLE [Runtime].[OrchestrationInstances]
                    ADD INDEX [IX_OrchestrationInstances_StartedOnUtc_Id]
                    NONCLUSTERED ([StartedOnUtc] DESC, [Id] DESC);');
        END;

        DECLARE @runtimeTable sysname;
        DECLARE @runtimeSql nvarchar(max);
        DECLARE runtime_identity_cursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT [TableName]
            FROM (VALUES
                (N'TaskExecutionAttempts'),
                (N'TaskDispatches')) AS RuntimeTables([TableName]);
        OPEN runtime_identity_cursor;
        FETCH NEXT FROM runtime_identity_cursor INTO @runtimeTable;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            IF COL_LENGTH(N'Runtime.' + @runtimeTable, N'StorageBucket') IS NOT NULL
            BEGIN
                SET @runtimeSql =
                    N' DROP INDEX ' + QUOTENAME(N'CIX_' + @runtimeTable + N'_StorageBucket_Id') +
                    N' ON [Runtime].' + QUOTENAME(@runtimeTable) + N';' +
                    N' ALTER TABLE [Runtime].' + QUOTENAME(@runtimeTable) +
                    N' DROP CONSTRAINT ' + QUOTENAME(N'PK_' + @runtimeTable) + N';' +
                    N' ALTER TABLE [Runtime].' + QUOTENAME(@runtimeTable) +
                    N' ADD CONSTRAINT ' + QUOTENAME(N'PK_' + @runtimeTable) +
                    N' PRIMARY KEY CLUSTERED ([Id]) WITH (FILLFACTOR = 100, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);' +
                    N' ALTER TABLE [Runtime].' + QUOTENAME(@runtimeTable) +
                    N' DROP COLUMN [StorageBucket];';
                EXEC sys.sp_executesql @runtimeSql;
            END;
            FETCH NEXT FROM runtime_identity_cursor INTO @runtimeTable;
        END;
        CLOSE runtime_identity_cursor;
        DEALLOCATE runtime_identity_cursor;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.extended_properties
            WHERE class = 0
              AND name = N'KrackendCompactRuntimeValuesInRowV2')
        BEGIN
            EXEC sys.sp_tableoption N'[Runtime].[TaskExecutionAttempts]',
                N'large value types out of row', N'OFF';
            EXEC sys.sp_tableoption N'[Runtime].[TaskDispatches]',
                N'large value types out of row', N'OFF';

            ALTER INDEX ALL ON [Runtime].[TaskExecutionAttempts]
                REBUILD WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW, SORT_IN_TEMPDB = ON, MAXDOP = 4);
            ALTER INDEX ALL ON [Runtime].[TaskDispatches]
                REBUILD WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW, SORT_IN_TEMPDB = ON, MAXDOP = 4);

            EXEC sys.sp_addextendedproperty
                @name = N'KrackendCompactRuntimeValuesInRowV2',
                @value = N'Enabled';
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.extended_properties
            WHERE class = 0
              AND name = N'KrackendRuntimeRowCompressionV1')
        BEGIN
            ALTER INDEX ALL ON [Runtime].[TaskExecutionAttempts]
                REBUILD WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW, SORT_IN_TEMPDB = ON, MAXDOP = 4);
            ALTER INDEX ALL ON [Runtime].[TaskDispatches]
                REBUILD WITH (FILLFACTOR = 95, DATA_COMPRESSION = ROW, SORT_IN_TEMPDB = ON, MAXDOP = 4);

            EXEC sys.sp_addextendedproperty
                @name = N'KrackendRuntimeRowCompressionV1',
                @value = N'Enabled';
        END;

        ALTER INDEX [PK_ExecutionTransitions] ON [Runtime].[ExecutionTransitions] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
        ALTER INDEX [PK_TaskDispatches] ON [Runtime].[TaskDispatches] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
        ALTER INDEX [PK_TaskExecutionAttempts] ON [Runtime].[TaskExecutionAttempts] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
        ALTER INDEX [CIX_TaskDispatches_StorageSequence] ON [Runtime].[TaskDispatches] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
        ALTER INDEX [CIX_TaskExecutionAttempts_StorageSequence] ON [Runtime].[TaskExecutionAttempts] SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
        DROP INDEX IF EXISTS [IX_SagaStatisticsObservations_Processed_InstanceId_Revision]
            ON [Runtime].[SagaStatisticsObservations];
        DROP INDEX IF EXISTS [IX_SagaStatisticsObservations_Pending_InstanceId_Revision]
            ON [Runtime].[SagaStatisticsObservations];
        CREATE INDEX [IX_SagaStatisticsObservations_Pending_InstanceId_Revision]
            ON [Runtime].[SagaStatisticsObservations] ([InstanceId], [Revision])
            WHERE [Processed] = 0;
        IF OBJECTPROPERTYEX(OBJECT_ID(N'[Runtime].[OrchestrationInstances]'), N'TableIsMemoryOptimized') = 0
        BEGIN
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_LastUpdatedOnUtc]
                ON [Runtime].[OrchestrationInstances];
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_Status_LastUpdatedOnUtc]
                ON [Runtime].[OrchestrationInstances];
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_SagaId]
                ON [Runtime].[OrchestrationInstances];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                AND name = N'IX_OrchestrationInstances_CorrelationId')
                CREATE INDEX [IX_OrchestrationInstances_CorrelationId]
                    ON [Runtime].[OrchestrationInstances] ([CorrelationId]);
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_StartedOnUtc_Status]
                ON [Runtime].[OrchestrationInstances];
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_StartedOnUtc_Id')
                CREATE INDEX [IX_OrchestrationInstances_StartedOnUtc_Id]
                    ON [Runtime].[OrchestrationInstances] ([StartedOnUtc] DESC, [Id] DESC)
                    WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_ExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances];
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_Status_ExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances];
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_Running_ExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances];
            CREATE INDEX [IX_OrchestrationInstances_Running_ExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances] ([ExpiresOnUtc], [Id])
                INCLUDE ([CorrelationId])
                WHERE [Status] = N'Running' AND [ExpiresOnUtc] IS NOT NULL
                WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);

            DROP INDEX IF EXISTS [IX_OrchestrationInstances_IdleExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances];
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_Status_IdleExpiresOnUtc]
                ON [Runtime].[OrchestrationInstances];
            DROP INDEX IF EXISTS [IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances];
            CREATE INDEX [IX_OrchestrationInstances_Idle_IdleExpiresOnUtc_Id]
                ON [Runtime].[OrchestrationInstances] ([IdleExpiresOnUtc], [Id])
                INCLUDE ([CorrelationId])
                WHERE [Status] = N'Idle' AND [IdleExpiresOnUtc] IS NOT NULL
                WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);

            DROP INDEX IF EXISTS [IX_OrchestrationInstances_Running_StartedOnUtc_Id]
                ON [Runtime].[OrchestrationInstances];
            CREATE INDEX [IX_OrchestrationInstances_Running_StartedOnUtc_Id]
                ON [Runtime].[OrchestrationInstances] ([StartedOnUtc], [Id])
                INCLUDE ([CorrelationId])
                WHERE [Status] = N'Running'
                WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[Runtime].[OrchestrationInstances]')
                  AND name = N'IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc'
                  AND has_filter = 1)
            BEGIN
                DROP INDEX IF EXISTS [IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc]
                    ON [Runtime].[OrchestrationInstances];
                CREATE INDEX [IX_OrchestrationInstances_DispatchCancellationPending_LastUpdatedOnUtc]
                    ON [Runtime].[OrchestrationInstances] ([DispatchCancellationPending], [LastUpdatedOnUtc])
                    WHERE [DispatchCancellationPending] = 1;
            END;
        END;

        IF OBJECTPROPERTYEX(OBJECT_ID(N'[Runtime].[TaskExecutions]'), N'TableIsMemoryOptimized') = 0
        BEGIN
            DROP INDEX IF EXISTS [IX_TaskExecutions_Status] ON [Runtime].[TaskExecutions];
            DROP INDEX IF EXISTS [IX_TaskExecutions_Status_ResponseDeadlineUtc] ON [Runtime].[TaskExecutions];
            DROP INDEX IF EXISTS [IX_TaskExecutions_Status_TimeoutActionId] ON [Runtime].[TaskExecutions];
            DROP INDEX IF EXISTS [IX_TaskExecutions_Status_CorrelationId_ResponseDeadlineUtc] ON [Runtime].[TaskExecutions];
            DROP INDEX IF EXISTS [IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id] ON [Runtime].[TaskExecutions];
            DROP INDEX IF EXISTS [IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id] ON [Runtime].[TaskExecutions];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Runtime].[TaskExecutions]')
                AND name = N'IX_TaskExecutions_CorrelationId')
                CREATE INDEX [IX_TaskExecutions_CorrelationId]
                    ON [Runtime].[TaskExecutions] ([CorrelationId]);
            CREATE INDEX [IX_TaskExecutions_Waiting_ResponseDeadlineUtc_Id]
                ON [Runtime].[TaskExecutions] ([ResponseDeadlineUtc], [Id])
                INCLUDE ([CorrelationId])
                WHERE [Status] = N'WaitingResponse' AND [ResponseDeadlineUtc] IS NOT NULL
                WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);
            CREATE INDEX [IX_TaskExecutions_LegacyWaiting_WaitingSinceUtc_Id]
                ON [Runtime].[TaskExecutions] ([WaitingSinceUtc], [Id])
                INCLUDE ([CorrelationId])
                WHERE [Status] = N'WaitingResponse' AND [ResponseDeadlineUtc] IS NULL
                WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF);
        END;

        DROP INDEX IF EXISTS [IX_TaskExecutionAttempts_Status] ON [Runtime].[TaskExecutionAttempts];
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Runtime].[TaskExecutionAttempts]')
            AND name = N'IX_TaskExecutionAttempts_DispatchId')
            CREATE INDEX [IX_TaskExecutionAttempts_DispatchId]
                ON [Runtime].[TaskExecutionAttempts] ([DispatchId]);
        DROP INDEX IF EXISTS [IX_TaskExecutionAttempts_Status_TaskExecutionId_WaitingSinceUtc]
            ON [Runtime].[TaskExecutionAttempts];

        DROP INDEX IF EXISTS [IX_TaskDispatches_DispatchStatus] ON [Runtime].[TaskDispatches];
        DROP INDEX IF EXISTS [IX_TaskDispatches_DispatchStatus_CorrelationId] ON [Runtime].[TaskDispatches];
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Runtime].[TaskDispatches]')
            AND name = N'IX_TaskDispatches_CommandId')
            CREATE INDEX [IX_TaskDispatches_CommandId]
                ON [Runtime].[TaskDispatches] ([CommandId]);
        DROP INDEX IF EXISTS [IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc]
            ON [Runtime].[TaskDispatches];
        CREATE INDEX [IX_TaskDispatches_QueuePending_Id_ScheduledOnUtc]
            ON [Runtime].[TaskDispatches] ([ScheduledOnUtc], [Id])
            WHERE [DispatchStatus] = N'QueuePending';

        DROP INDEX IF EXISTS [IX_TaskDispatches_PhysicalCancellationPending]
            ON [Runtime].[TaskDispatches];
        CREATE INDEX [IX_TaskDispatches_PhysicalCancellationPending]
            ON [Runtime].[TaskDispatches] ([PhysicalCancellationPending])
            WHERE [PhysicalCancellationPending] = 1;

        DROP INDEX IF EXISTS [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc]
            ON [Runtime].[TaskDispatches];
        CREATE INDEX [IX_TaskDispatches_CallbackPending_CallbackAcceptedOnUtc]
            ON [Runtime].[TaskDispatches] ([CallbackPending], [CallbackAcceptedOnUtc])
            WHERE [CallbackPending] = 1;
        """);
    }
    else if (runtimeDb.Database.IsNpgsql())
    {
        // EnsureCreated does not reconcile indexes for an existing database.
        // The revision lookup is only used by a one-time legacy backfill. Keeping
        // even a partial index prevents HOT updates because its predicate refers
        // to StatisticsRevision, which advances on every callback.
        await runtimeDb.Database.ExecuteSqlRawAsync("""
            DROP INDEX IF EXISTS "Runtime"."IX_OrchestrationInstances_StatisticsRevision";
            CREATE INDEX IF NOT EXISTS "IX_OrchestrationInstances_StartedOnUtc_Id"
                ON "Runtime"."OrchestrationInstances" ("StartedOnUtc", "Id");
            ALTER TABLE "Runtime"."OrchestrationInstances" SET (fillfactor = 30);
            ALTER TABLE "Runtime"."TaskExecutions" SET (fillfactor = 70);
            ALTER TABLE "Runtime"."StageExecutions" SET (fillfactor = 80);
            """);
    }
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

static async Task EnsureSqlServerMemoryOptimizedDatabaseAsync(string connectionString)
{
    var target = new SqlConnectionStringBuilder(connectionString);
    var databaseName = target.InitialCatalog;
    if (string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("The SQL Server runtime database name is required.");

    target.InitialCatalog = "master";
    target.MinPoolSize = 0;
    target.Pooling = false;
    await using var connection = new SqlConnection(target.ConnectionString);
    await connection.OpenAsync(CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        DECLARE @databaseName sysname = @name;
        DECLARE @sql nvarchar(max);
        DECLARE @memoryFilePath nvarchar(4000) =
            CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) + @databaseName + N'_memory';
        IF DB_ID(@databaseName) IS NULL
        BEGIN
            SET @sql = N'CREATE DATABASE ' + QUOTENAME(@databaseName) + N';';
            EXEC sys.sp_executesql @sql;
        END;

        SET @sql = N'IF NOT EXISTS (SELECT 1 FROM ' + QUOTENAME(@databaseName) +
            N'.sys.filegroups WHERE [type] = ''FX'') BEGIN ' +
            N'ALTER DATABASE ' + QUOTENAME(@databaseName) +
            N' ADD FILEGROUP [RuntimeMemoryOptimized] CONTAINS MEMORY_OPTIMIZED_DATA; ' +
            N'ALTER DATABASE ' + QUOTENAME(@databaseName) +
            N' ADD FILE (NAME = ' + QUOTENAME(@databaseName + N'_memory', '''') +
            N', FILENAME = ' + QUOTENAME(@memoryFilePath, '''') +
            N') TO FILEGROUP [RuntimeMemoryOptimized]; END;';
        EXEC sys.sp_executesql @sql;
        """;
    command.Parameters.AddWithValue("@name", databaseName);
    await command.ExecuteNonQueryAsync(CancellationToken.None);
}

