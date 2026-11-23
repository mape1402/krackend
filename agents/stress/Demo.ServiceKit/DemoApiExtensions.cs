using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mule;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Management;
namespace Krackend.Demo;

public sealed class DemoSeed
{
    public string TestRunId { get; set; } = "";
    public string SaleId { get; set; } = "";
    public string Status { get; set; } = "Confirmed";
    public decimal Total { get; set; }
    public List<DemoLine> Lines { get; set; } = [];
    public int OnHand { get; set; } = 10000;
    public bool CreateReservation { get; set; }
    public bool Captured { get; set; }
    public bool CommitReservation { get; set; }
    public DateTime? ExpiresOnUtc { get; set; }
}
public static class DemoApiExtensions
{
    public static IServiceCollection AddDemoApi(this IServiceCollection services, IConfiguration config, string service)
    {
        var minimumThreads = Math.Max(
            Environment.ProcessorCount * 2,
            config.GetValue("Demo:ThreadPoolMinThreads", 32));
        ThreadPool.GetMinThreads(out var currentWorkerThreads, out var currentIoThreads);
        ThreadPool.SetMinThreads(
            Math.Max(currentWorkerThreads, minimumThreads),
            Math.Max(currentIoThreads, minimumThreads));

        services.Replace(ServiceDescriptor.Singleton<IConsumingManager,
            DemoDirectParallelConsumingManager>());
        // The replacement consumer invokes every async handler directly. These
        // settings retain unlimited broker delivery for adapters that inspect them;
        // there is no application handoff queue or handler concurrency semaphore.
        services.PostConfigure<GlobalSettings>(settings =>
        {
            settings.ConsumerExecution.MaxConcurrency = null;
            settings.ConsumerExecution.QueueCapacity = int.MaxValue;
            settings.ConsumerExecution.PrefetchCount = null;
        });
        services.AddSingleton(new DemoServiceIdentity(service));
        services.AddSingleton<DemoTriggerAcceptanceCache>();
        services.AddSingleton(new DemoCommandObservations(config.GetValue("Demo:TestingEnabled", false)));
        services.AddDbContextPool<DemoDbContext>(options => options.UseSqlServer(
            config.GetConnectionString("Demo") ?? throw new InvalidOperationException("Set ConnectionStrings:Demo to the isolated demo SQL database.")));
        services.AddScoped<DemoBusinessStore>();
        services.AddScoped<IDemoBusinessStore>(p => p.GetRequiredService<DemoBusinessStore>());
        services.AddScoped<DemoOutbox>();
        services.AddScoped<DemoEmailDeliveryExecutor>();
        services.AddScoped<DemoService>();
        services.AddMule(mule =>
        {
            mule.UseEntityFrameworkCore<DemoDbContext>(storage => storage.ConfigureActions(actions =>
            {
                // Recovery indexes contain active work only. Keeping every completed
                // action in all four status trees made each healthy completion rewrite
                // four global B-trees and created a cross-saga page-latch convoy.
                actions.HasIndex(x => new { x.Lane, x.Status, x.NextAttemptOnUtc, x.CreatedOnUtc })
                    .HasFilter("[Status] <> 2");
                actions.HasIndex(x => new { x.Status, x.PendingReason, x.NextAttemptOnUtc })
                    .HasFilter("[Status] <> 2");
                actions.HasIndex(x => new { x.Status, x.LockedOnUtc })
                    .HasFilter("[Status] <> 2");
                actions.HasIndex(x => new { x.Status, x.CompletedOnUtc })
                    .HasFilter("[Status] = 2");
            }));
            mule.AddActionsFromAssemblyContaining<DemoPublishAction>();
        });
        services.Configure<MuleSettings>(settings =>
        {
            var processors = Math.Max(1, Environment.ProcessorCount);
            settings.DispatchInterval = TimeSpan.FromSeconds(1);
            settings.ImmediateDispatch = true;
            settings.RecoveryMode = MuleRecoveryMode.Scheduled;
            settings.CleanupMode = MuleCleanupMode.Scheduled;
            settings.CleanupInterval = TimeSpan.FromSeconds(10);
            settings.CompletedRetention = TimeSpan.FromMinutes(1);
            settings.CleanupBatchSize = 5_000;
            settings.LockTimeout = TimeSpan.FromSeconds(Math.Max(1, config.GetValue("Demo:LockTimeoutSeconds", 15)));
            var concurrency = Math.Max(1, config.GetValue(
                "Demo:OutboxConcurrency",
                Math.Clamp(processors * 4, 32, 128)));
            settings.MaxDegreeOfParallelism = concurrency;
            settings.DispatchQueueCapacity = Math.Max(512, concurrency * 16);
            settings.ExecutionQueueCapacity = Math.Max(512, concurrency * 16);
            var workerCount = Math.Max(2, config.GetValue(
                "Demo:OutboxWorkers",
                Math.Clamp(processors, 8, 32)));
            foreach (var lane in new[] { "demo-publish", "demo-email" })
                settings.Lanes[lane] = new MuleLaneSettings { WorkerCount = workerCount,
                    MaxDegreeOfParallelism = concurrency,
                    DispatchQueueCapacity = Math.Max(512, concurrency * 16),
                    ExecutionQueueCapacity = Math.Max(512, concurrency * 16),
                    DispatchBatchSize = Math.Min(256, Math.Max(64, concurrency * 2)) };
        });
        return services;
    }
    public static async Task InitializeDemoAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
        await db.Database.EnsureCreatedAsync();
        var databaseName = db.Database.GetDbConnection().Database;
        if (string.IsNullOrWhiteSpace(databaseName) ||
            databaseName.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            throw new InvalidOperationException("The demo SQL database name contains unsupported characters.");
        // These isolated load-test databases do not take transaction-log backups.
        // FULL recovery would therefore retain every committed log record forever,
        // eventually forcing synchronous file growth in the hot path.
        await db.Database.ExecuteSqlRawAsync(
            $"ALTER DATABASE [{databaseName}] SET RECOVERY SIMPLE; " +
            // Indirect checkpoints smooth dirty-page flushing across the sustained
            // parallel workload. TARGET_RECOVERY_TIME=0 lets automatic checkpoints
            // arrive as large FLUSHFILEBUFFERS stalls that stop otherwise independent
            // service transactions at the same time.
            $"ALTER DATABASE [{databaseName}] SET TARGET_RECOVERY_TIME = 300 SECONDS; CHECKPOINT;");
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[Demo].[DurableAction]') IS NOT NULL
            BEGIN
                ALTER INDEX [PK_DurableAction] ON [Demo].[DurableAction]
                    SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Demo].[DurableAction]')
                    AND name = N'IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc' AND has_filter = 0)
                BEGIN
                    DROP INDEX [IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc] ON [Demo].[DurableAction];
                    CREATE INDEX [IX_DurableAction_Lane_Status_NextAttemptOnUtc_CreatedOnUtc]
                        ON [Demo].[DurableAction] ([Lane], [Status], [NextAttemptOnUtc], [CreatedOnUtc])
                        WHERE [Status] <> 2 WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
                END;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Demo].[DurableAction]')
                    AND name = N'IX_DurableAction_Status_PendingReason_NextAttemptOnUtc' AND has_filter = 0)
                BEGIN
                    DROP INDEX [IX_DurableAction_Status_PendingReason_NextAttemptOnUtc] ON [Demo].[DurableAction];
                    CREATE INDEX [IX_DurableAction_Status_PendingReason_NextAttemptOnUtc]
                        ON [Demo].[DurableAction] ([Status], [PendingReason], [NextAttemptOnUtc])
                        WHERE [Status] <> 2 WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
                END;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Demo].[DurableAction]')
                    AND name = N'IX_DurableAction_Status_LockedOnUtc' AND has_filter = 0)
                BEGIN
                    DROP INDEX [IX_DurableAction_Status_LockedOnUtc] ON [Demo].[DurableAction];
                    CREATE INDEX [IX_DurableAction_Status_LockedOnUtc]
                        ON [Demo].[DurableAction] ([Status], [LockedOnUtc])
                        WHERE [Status] <> 2 WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
                END;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Demo].[DurableAction]')
                    AND name = N'IX_DurableAction_Status_CompletedOnUtc' AND has_filter = 0)
                BEGIN
                    DROP INDEX [IX_DurableAction_Status_CompletedOnUtc] ON [Demo].[DurableAction];
                    CREATE INDEX [IX_DurableAction_Status_CompletedOnUtc]
                        ON [Demo].[DurableAction] ([Status], [CompletedOnUtc])
                        WHERE [Status] = 2 WITH (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
                END;
            END;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @sequentialIndexes nvarchar(max) = N'';
            SELECT @sequentialIndexes = @sequentialIndexes
                + N'ALTER INDEX ' + QUOTENAME(i.[name])
                + N' ON ' + QUOTENAME(s.[name]) + N'.' + QUOTENAME(t.[name])
                + N' SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);'
            FROM sys.indexes AS i
            INNER JOIN sys.tables AS t ON t.[object_id] = i.[object_id]
            INNER JOIN sys.schemas AS s ON s.[schema_id] = t.[schema_id]
            WHERE s.[name] = N'Demo' AND i.is_primary_key = 1;
            IF LEN(@sequentialIndexes) > 0
                EXEC sys.sp_executesql @sequentialIndexes;

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [object_id] = OBJECT_ID(N'[Demo].[Operations]')
                  AND [name] = N'IX_Operations_TestRunId_RequestId')
                ALTER INDEX [IX_Operations_TestRunId_RequestId] ON [Demo].[Operations]
                    SET (OPTIMIZE_FOR_SEQUENTIAL_KEY = ON);
            """);
        if (scope.ServiceProvider.GetRequiredService<DemoServiceIdentity>().Name == "inventories" &&
            !await db.Stock.AnyAsync(x => x.Id == "demo-sku"))
        {
            db.Stock.Add(new DemoStock { Id = "demo-sku", TestRunId = "legacy", OnHand = 100000 });
            await db.SaveChangesAsync();
        }

        var warmCount = Math.Max(0, app.Configuration.GetValue("Demo:SqlPoolWarmConnections", 16));
        if (warmCount > 0)
        {
            var connectionString = db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The demo SQL connection string is missing.");
            var connections = Enumerable.Range(0, warmCount)
                .Select(_ => new SqlConnection(connectionString))
                .ToArray();
            try
            {
                await Task.WhenAll(connections.Select(connection => connection.OpenAsync()));
            }
            finally
            {
                await Task.WhenAll(connections.Select(connection => connection.DisposeAsync().AsTask()));
            }
        }
    }
    public static WebApplication MapDemoEndpoints(this WebApplication app, string service)
    {
        foreach (var definition in DemoCases.All.Where(x => x.Service == service))
            app.MapPost(definition.Route, async (HttpContext context, DemoService handler, CancellationToken ct) =>
            {
                try
                {
                    var request = await context.Request.ReadFromJsonAsync<DemoRequest>(DemoJson.Options, ct);
                    if (request is null) return Results.BadRequest("A JSON request is required.");
                    var routeId = context.Request.RouteValues.GetValueOrDefault("saleId")?.ToString()
                        ?? context.Request.RouteValues.GetValueOrDefault("reservationId")?.ToString();
                    if (routeId is not null)
                    {
                        if (request.SaleId.Length > 0 && request.SaleId != routeId) return Results.BadRequest("SaleId disagrees with the route.");
                        request.SaleId = routeId;
                    }
                    var result = await handler.AcceptAsync(definition.Id, request, ct, context.Request.Query["via"] == "consumer");
                    return Results.Accepted("/demo/requests/" + result.RequestId, result);
                }
                catch (ValidationException e) { return Results.BadRequest(new { error = e.Message }); }
            }).WithName(definition.Id + "_" + service);
        app.MapGet("/demo/cases", () => DemoCases.All.Where(x => x.Service == service));
        if (app.Configuration.GetValue("Demo:TestingEnabled", false))
        {
            app.MapGet("/demo/testing/observations/{requestId}", (string requestId, DemoCommandObservations observations) => observations.Read(requestId));
            app.MapGet("/demo/testing/outbox/{requestId}", async (string requestId, DemoDbContext db, CancellationToken ct) =>
                await db.Set<DurableAction>().AsNoTracking().Where(x => x.CorrelationId == requestId)
                    .Select(x => new { x.Id, Key = x.Key.ToString(), Status = x.Status.ToString(), x.Attempts, x.LastError, x.CreatedOnUtc }).ToArrayAsync(ct));
            app.MapPost("/demo/testing/replies/{requestId}/replay", async (string requestId, DemoReplayRequest replay,
                DemoDbContext db, DemoOutbox outbox, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(replay.ReplayId) || replay.ReplayId.Length > 64 || string.IsNullOrWhiteSpace(replay.TestRunId))
                    return Results.BadRequest("ReplayId and TestRunId are required.");
                var belongsToRun = await db.Operations.AnyAsync(x => x.RequestId == requestId && x.TestRunId == replay.TestRunId, ct)
                    || await db.Emails.AnyAsync(x => x.Id == requestId + ":email" && x.TestRunId == replay.TestRunId, ct);
                if (!belongsToRun) return Results.Ok(new { replayed = 0 });
                var key = ActionKey.From("DemoPublish");
                var originals = await db.Set<DurableAction>().AsNoTracking().Where(x => x.CorrelationId == requestId && x.Key == key &&
                    (x.DeduplicationKey == null || !x.DeduplicationKey.StartsWith("test-replay:"))).ToArrayAsync(ct);
                var callbacks = originals.Select(x => new { x.Id, Publication = DemoJson.Read<DemoPublication>(x.Payload) })
                    .Where(x => x.Publication.Result is not null).ToArray();
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    foreach (var callback in callbacks)
                        await outbox.AddAsync("DemoPublish", "test-replay:" + replay.ReplayId + ":" + callback.Id,
                            callback.Publication, requestId, ct);
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                await outbox.NotifyCommittedAsync(ct);
                return Results.Ok(new { replayed = callbacks.Length });
            });
        }
        app.MapGet("/demo/requests/{requestId}", async (string requestId, DemoDbContext db, CancellationToken ct) =>
            await db.Operations.AsNoTracking().Where(x => x.RequestId == requestId).ToArrayAsync(ct));
        app.MapGet("/demo/results/{testRunId}", async (string testRunId, DemoDbContext db, CancellationToken ct) => new
        {
            service,
            operations = await db.Operations.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct),
            sales = await db.Sales.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct),
            stock = await db.Stock.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct),
            reservations = await db.Reservations.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct),
            payments = await db.Payments.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct),
            entries = await db.Entries.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct),
            emails = await db.Emails.AsNoTracking().Where(x => x.TestRunId == testRunId).ToArrayAsync(ct)
        });
        app.MapPost("/demo/seed", async (DemoSeed seed, DemoDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(seed.TestRunId) || seed.TestRunId.Length > 96 || seed.SaleId.Length > 96 ||
                seed.OnHand < 0 || seed.Total < 0 || seed.Lines.Any(x => x.Quantity <= 0 || x.Sku.Length > 96 || x.UnitPrice < 0))
                return Results.BadRequest("Invalid fixture.");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (service == "sales" && seed.SaleId.Length > 0 && !await db.Sales.AnyAsync(x => x.Id == seed.SaleId, ct))
                db.Sales.Add(new DemoSale { Id = seed.SaleId, TestRunId = seed.TestRunId, Status = seed.Status,
                    Total = seed.Total, LinesJson = DemoJson.Write(seed.Lines), ExpiresOnUtc = seed.ExpiresOnUtc });
            if (service == "payments" && seed.SaleId.Length > 0 && !await db.Payments.AnyAsync(x => x.Id == seed.SaleId, ct))
                db.Payments.Add(new DemoPayment { Id = seed.SaleId, TestRunId = seed.TestRunId,
                    IntentStatus = seed.Captured ? "Captured" : "Pending", Captured = seed.Captured ? seed.Total : 0, ExpectedTotal = seed.Total });
            if (service == "inventories")
            {
                foreach (var line in seed.Lines)
                    if (!await db.Stock.AnyAsync(x => x.Id == line.Sku, ct))
                        db.Stock.Add(new DemoStock { Id = line.Sku, TestRunId = seed.TestRunId, OnHand = seed.OnHand });
                await db.SaveChangesAsync(ct);
                if (seed.CreateReservation && !await db.Reservations.AnyAsync(x => x.Id == "sale:" + seed.SaleId, ct))
                {
                    foreach (var line in seed.Lines)
                        if ((seed.CommitReservation
                            ? await db.Stock.Where(x => x.Id == line.Sku && x.OnHand - x.Reserved >= line.Quantity)
                                .ExecuteUpdateAsync(u => u.SetProperty(x => x.OnHand, x => x.OnHand - line.Quantity), ct)
                            : await db.Stock.Where(x => x.Id == line.Sku && x.OnHand - x.Reserved >= line.Quantity)
                                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Reserved, x => x.Reserved + line.Quantity), ct)) != 1)
                            throw new InvalidOperationException("Insufficient fixture stock.");
                    db.Reservations.Add(new DemoReservation { Id = "sale:" + seed.SaleId, SaleId = seed.SaleId, TestRunId = seed.TestRunId,
                        LinesJson = DemoJson.Write(seed.Lines), ExpiresOnUtc = seed.ExpiresOnUtc,
                        Status = seed.CommitReservation ? "Committed" : "Reserved" });
                }
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { service, seed.TestRunId, seed.SaleId });
        });
        return app;
    }
}

public sealed record DemoReplayRequest(string ReplayId, string TestRunId);

