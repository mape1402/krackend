using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;
using Krackend.Sagas.Orchestrations.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.Web;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Mule;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Topology;

var builder = WebApplication.CreateBuilder(args);
var environmentKey = builder.Configuration["Runtime:EnvironmentKey"] ?? "local";
var runtimeConnection = builder.Configuration.GetConnectionString("Runtime");
if (string.IsNullOrWhiteSpace(runtimeConnection))
    throw new InvalidOperationException("Set ConnectionStrings:Runtime for the Runtime host.");

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks()
    .AddAsyncCheck("sql", async () =>
    {
        await using var connection = new SqlConnection(runtimeConnection);
        await connection.OpenAsync();
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
    }, tags: ["ready"]);

builder.Services.AddHttpClient();
builder.Services.AddKrackendSagasOrchestrationsRuntime(options => options.EnvironmentKey = environmentKey);
builder.Services.AddKrackendSagasOrchestrationsWeb(options =>
{
    options.DistributionBaseUri = builder.Configuration["Runtime:ArtifactPull:DistributionBaseUri"];
    options.RuntimeNodeId = builder.Configuration["Runtime:ArtifactPull:RuntimeNodeId"];
});
builder.Services.AddKrackendSagasOrchestrationsSqlServer(db => db.UseSqlServer(runtimeConnection));
builder.Services.AddMule(mule =>
{
    mule
        .UseKrackendSagasOrchestrationsRuntimeStorage()
        .UseFastLaneInMemory(options =>
        {
            options.IntentFlushSize = builder.Configuration.GetValue("Mule:FastLane:IntentFlushSize", 2_000);
            options.CompletionFlushSize = builder.Configuration.GetValue("Mule:FastLane:CompletionFlushSize", 5_000);
            options.FlushInterval = TimeSpan.FromMilliseconds(builder.Configuration.GetValue("Mule:FastLane:FlushIntervalMilliseconds", 10));
        });
});
builder.Services.AddKrackendSagasOrchestrationsEngine();
builder.Services.AddKrackendSagasOrchestrationsRuntimeRecovery(options =>
{
    options.Enabled = true;
    options.RunOnStartup = true;
    options.ScanInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue("Runtime:Recovery:ScanIntervalSeconds", 10));
    options.MinimumScanInterval = TimeSpan.FromSeconds(1);
});
builder.Services.AddKrackendSagasOrchestrationsMuleDurableWork();
builder.Services.AddOrchestratorRuntimeWebUI(options => options.RoutePrefix = "runtime");

builder.Services.AddKrackendSagasOrchestrationsMessagingPigeon(builder.Configuration, settings =>
{
    settings.SetDomain("Krackend.Sagas.Orchestrations.RuntimeHost.Sample");
    settings.ConfigureConsumerExecution(consumer =>
    {
        consumer.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
    });
    settings.SetTopologyProvisioningMode(
        TopologyProvisioningMode.OnStartup |
        TopologyProvisioningMode.OnConsume);
    settings.UseRabbitMq(rabbit =>
    {
        rabbit.Url = builder.Configuration["RabbitMq:Url"];
        rabbit.Exchange = builder.Configuration["RabbitMq:Exchange"];
        rabbit.ExchangeType = builder.Configuration["RabbitMq:ExchangeType"] ?? "direct";
        rabbit.DurableExchange = builder.Configuration.GetValue("RabbitMq:DurableExchange", false);
    });
});

var app = builder.Build();

if (builder.Configuration.GetValue("Demo:EnsureCreated", true))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<RuntimeStorageDbContext>().Database.EnsureCreated();
}

app.MapStaticAssets();
app.MapGet("/", (RuntimeEnvironmentDescriptor runtime) => Results.Ok(new { service = "Krackend.Sagas.Orchestrations.RuntimeHost.Sample", runtime.EnvironmentKey }));

app.MapPost("/demo/deploy-from-design", async (IHttpClientFactory clients, IRuntimeArtifactDeploymentService service, IConfiguration config, CancellationToken ct) =>
{
    var designBaseUri = config["Demo:DesignBaseUri"] ?? "http://localhost:5101";
    var artifact = await clients.CreateClient().GetFromJsonAsync<RuntimeArtifactDeploymentRequest>($"{designBaseUri.TrimEnd('/')}/demo/artifact/order-fulfillment", ct);
    var result = await service.Deploy(artifact, ct);
    return result.Accepted ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/demo/deploy-from-design/{artifactKey}/{version}", async (
    string artifactKey,
    string version,
    IHttpClientFactory clients,
    IRuntimeArtifactDeploymentService service,
    IConfiguration config,
    CancellationToken ct) =>
{
    var designBaseUri = config["Demo:DesignBaseUri"] ?? "http://localhost:5101";
    var artifact = await clients.CreateClient().GetFromJsonAsync<RuntimeArtifactDeploymentRequest>($"{designBaseUri.TrimEnd('/')}/demo/artifact/{artifactKey}/{version}", ct);
    var result = await service.Deploy(artifact, ct);
    return result.Accepted ? Results.Ok(result) : Results.BadRequest(result);
});

app.MapPost("/demo/reset", async (RuntimeStorageDbContext db, CancellationToken ct) =>
{
    await db.TaskDispatches.ExecuteDeleteAsync(ct);
    await db.TaskExecutionAttempts.ExecuteDeleteAsync(ct);
    await db.CompensationExecutions.ExecuteDeleteAsync(ct);
    await db.ExecutionTransitions.ExecuteDeleteAsync(ct);
    await db.InstanceVariables.ExecuteDeleteAsync(ct);
    await db.TaskExecutions.ExecuteDeleteAsync(ct);
    await db.StageExecutions.ExecuteDeleteAsync(ct);
    await db.OrchestrationInstances.ExecuteDeleteAsync(ct);
    await db.TriggerIntakeAttempts.ExecuteDeleteAsync(ct);
    await db.TriggerIntakes.ExecuteDeleteAsync(ct);
    await db.EnvironmentVariables.ExecuteDeleteAsync(ct);
    await db.Artifacts.ExecuteDeleteAsync(ct);
    return Results.Ok(new { status = "Reset" });
});

app.MapPost("/demo/reset-schema", async (RuntimeStorageDbContext db, CancellationToken ct) =>
{
    await db.Database.EnsureDeletedAsync(ct);
    await db.Database.EnsureCreatedAsync(ct);
    return Results.Ok(new { status = "SchemaReset" });
});

app.MapGet("/demo/stats", async (string prefix, RuntimeStorageDbContext db, CancellationToken ct) =>
{
    var query = db.OrchestrationInstances.AsNoTracking()
        .Where(x => string.IsNullOrWhiteSpace(prefix) || x.CorrelationId.StartsWith(prefix));

    var total = await query.CountAsync(ct);
    var running = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Running, ct);
    var waiting = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Waiting, ct);
    var compensating = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Compensating, ct);
    var compensated = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Compensated, ct);
    var completed = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Completed, ct);
    var completedWithErrors = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.CompletedWithErrors, ct);
    var failed = await query.CountAsync(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Failed, ct);
    var terminal = completed + completedWithErrors + failed + compensated;
    var firstStarted = await query.MinAsync(x => (DateTime?)x.StartedOnUtc, ct);
    var lastUpdated = await query.MaxAsync(x => (DateTime?)x.LastUpdatedOnUtc, ct);

    return Results.Ok(new
    {
        prefix,
        total,
        running,
        waiting,
        compensating,
        compensated,
        completed,
        completedWithErrors,
        failed,
        terminal,
        nonTerminal = total - terminal,
        firstStarted,
        lastUpdated
    });
});

app.MapGet("/demo/stress/summary", async (string prefix, int expected, RuntimeStorageDbContext db, CancellationToken ct) =>
{
    var rows = await db.OrchestrationInstances.AsNoTracking()
        .Where(x => string.IsNullOrWhiteSpace(prefix) || x.CorrelationId.StartsWith(prefix))
        .Select(x => new
        {
            x.CorrelationId,
            x.Status,
            x.StartedOnUtc,
            x.CompletedOnUtc,
            x.FailedOnUtc,
            x.CompensatedOnUtc,
            x.LastUpdatedOnUtc
        })
        .ToArrayAsync(ct);

    var terminalStatuses = new[]
    {
        Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Completed,
        Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.CompletedWithErrors,
        Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Failed,
        Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Compensated
    };

    var statusBreakdown = rows
        .GroupBy(x => x.Status.ToString())
        .OrderBy(x => x.Key)
        .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
    var duplicateCorrelationIds = rows
        .GroupBy(x => x.CorrelationId, StringComparer.OrdinalIgnoreCase)
        .Where(x => x.Count() > 1)
        .Select(x => new { correlationId = x.Key, count = x.Count() })
        .OrderByDescending(x => x.count)
        .ThenBy(x => x.correlationId)
        .Take(50)
        .ToArray();
    var zombieSamples = rows
        .Where(x => !terminalStatuses.Contains(x.Status))
        .OrderBy(x => x.StartedOnUtc)
        .Take(50)
        .Select(x => new
        {
            x.CorrelationId,
            Status = x.Status.ToString(),
            x.StartedOnUtc,
            x.LastUpdatedOnUtc
        })
        .ToArray();
    var latencies = rows
        .Where(x => terminalStatuses.Contains(x.Status))
        .Select(x => new
        {
            x.StartedOnUtc,
            FinishedOnUtc = x.CompletedOnUtc ?? x.FailedOnUtc ?? x.CompensatedOnUtc ?? x.LastUpdatedOnUtc
        })
        .Where(x => x.FinishedOnUtc >= x.StartedOnUtc)
        .Select(x => (x.FinishedOnUtc - x.StartedOnUtc).TotalMilliseconds)
        .OrderBy(x => x)
        .ToArray();

    var total = rows.Length;
    var completed = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Completed);
    var completedWithErrors = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.CompletedWithErrors);
    var failed = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Failed);
    var compensated = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Compensated);
    var compensating = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Compensating);
    var running = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Running);
    var waiting = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Waiting);
    var created = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Created);
    var stopped = rows.Count(x => x.Status == Krackend.Sagas.Orchestrations.Abstractions.Runtime.OrchestrationInstanceStatus.Stopped);
    var terminal = completed + completedWithErrors + failed + compensated;
    var nonTerminal = total - terminal;
    var muleActions = await db.Set<DurableAction>()
        .AsNoTracking()
        .Where(x => string.IsNullOrWhiteSpace(prefix) || x.CorrelationId.StartsWith(prefix))
        .Select(x => new
        {
            Key = x.Key.Value,
            x.Lane,
            x.Status,
            x.CreatedOnUtc,
            x.LockedOnUtc,
            x.StartedOnUtc,
            x.CompletedOnUtc,
            x.TerminalOnUtc
        })
        .ToArrayAsync(ct);
    var mulePending = muleActions.Count(x => x.Status == DurableActionStatus.Pending);
    var muleLocked = muleActions.Count(x => x.Status == DurableActionStatus.Locked);
    var muleCompleted = muleActions.Count(x => x.Status == DurableActionStatus.Completed);
    var muleFailed = muleActions.Count(x => x.Status == DurableActionStatus.Failed);
    var now = DateTimeOffset.UtcNow;
    var muleTerminalDurations = muleActions
        .Where(x => x.TerminalOnUtc != null)
        .Select(x => (x.TerminalOnUtc!.Value - x.CreatedOnUtc).TotalMilliseconds)
        .OrderBy(x => x)
        .ToArray();

    return Results.Ok(new
    {
        prefix,
        expected,
        total,
        accepted = total,
        terminal,
        completed,
        completedWithErrors,
        failed,
        compensated,
        compensating,
        running,
        waiting,
        created,
        stopped,
        nonTerminal,
        missing = Math.Max(expected - total, 0),
        duplicatedCorrelationIds = duplicateCorrelationIds.Length,
        duplicateSamples = duplicateCorrelationIds,
        zombieInstances = nonTerminal,
        zombieSamples,
        firstStarted = rows.Length == 0 ? null : rows.Min(x => (DateTime?)x.StartedOnUtc),
        lastUpdated = rows.Length == 0 ? null : rows.Max(x => (DateTime?)x.LastUpdatedOnUtc),
        statusBreakdown,
        durableWork = new
        {
            actions = new
            {
                total = muleActions.Length,
                pending = mulePending,
                locked = muleLocked,
                completed = muleCompleted,
                failed = muleFailed,
                nonTerminal = mulePending + muleLocked,
                actionsPerAcceptedInstance = total == 0 ? 0 : Math.Round((double)muleActions.Length / total, 2),
                pendingPerZombieInstance = nonTerminal == 0 ? 0 : Math.Round((double)mulePending / nonTerminal, 2),
                oldestPendingAgeSeconds = DemoStressMetrics.MaxAgeSeconds(muleActions.Where(x => x.Status == DurableActionStatus.Pending).Select(x => (DateTimeOffset?)x.CreatedOnUtc), now),
                oldestLockedAgeSeconds = DemoStressMetrics.MaxAgeSeconds(muleActions.Where(x => x.Status == DurableActionStatus.Locked).Select(x => x.LockedOnUtc), now),
                terminalLatencyMs = new
                {
                    count = muleTerminalDurations.Length,
                    min = DemoStressMetrics.Percentile(muleTerminalDurations, 0),
                    p50 = DemoStressMetrics.Percentile(muleTerminalDurations, 50),
                    p95 = DemoStressMetrics.Percentile(muleTerminalDurations, 95),
                    p99 = DemoStressMetrics.Percentile(muleTerminalDurations, 99),
                    max = DemoStressMetrics.Percentile(muleTerminalDurations, 100)
                }
            },
            byLane = muleActions
                .GroupBy(x => x.Lane ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key)
                .Select(x => new
                {
                    lane = x.Key,
                    total = x.Count(),
                    pending = x.Count(y => y.Status == DurableActionStatus.Pending),
                    locked = x.Count(y => y.Status == DurableActionStatus.Locked),
                    completed = x.Count(y => y.Status == DurableActionStatus.Completed),
                    failed = x.Count(y => y.Status == DurableActionStatus.Failed)
                })
                .ToArray(),
            byAction = muleActions
                .GroupBy(x => x.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key)
                .Select(x => new
                {
                    action = x.Key,
                    total = x.Count(),
                    pending = x.Count(y => y.Status == DurableActionStatus.Pending),
                    locked = x.Count(y => y.Status == DurableActionStatus.Locked),
                    completed = x.Count(y => y.Status == DurableActionStatus.Completed),
                    failed = x.Count(y => y.Status == DurableActionStatus.Failed)
                })
                .ToArray()
        },
        latencyMs = new
        {
            count = latencies.Length,
            min = DemoStressMetrics.Percentile(latencies, 0),
            p50 = DemoStressMetrics.Percentile(latencies, 50),
            p95 = DemoStressMetrics.Percentile(latencies, 95),
            p99 = DemoStressMetrics.Percentile(latencies, 99),
            max = DemoStressMetrics.Percentile(latencies, 100)
        }
    });
});


app.MapPost("/demo/run-order", async (IRuntimeTriggerInteractionService service, CancellationToken ct) =>
{
    var orderId = $"order-{Ulid.NewUlid()}";
    var trigger = await service.Enqueue(new RuntimeTriggerRequest
    {
        TriggerType = "Event",
        TriggerKey = "order.fulfillment",
        ArtifactVersion = "1.0.0",
        EnvironmentKey = "local",
        CorrelationId = orderId,
        IdempotencyKey = orderId,
        SourceMessageId = orderId,
        PayloadJson = JsonSerializer.Serialize(new { OrderId = orderId, Sku = "SKU-001", Quantity = 2, Amount = 149.99m })
    }, ct);

    return Results.Ok(new { trigger });
});

app.MapPost("/demo/pending/process", async (IRuntimePendingWorkProcessor processor, CancellationToken ct) =>
{
    var result = await processor.ProcessDueWork(DateTime.UtcNow, ct);
    return Results.Ok(result);
});

app.MapPost("/demo/backchannel", async (DemoBackChannelEnvelope envelope, IRuntimeBackChannelResponseHandler handler, CancellationToken ct) =>
{
    await handler.Handle(new MessageConsumeContext
    {
        Topic = envelope.Topic,
        Version = envelope.Version,
        CreatedOnUtc = DateTimeOffset.UtcNow,
        Message = JsonSerializer.SerializeToNode(envelope.Payload),
        Metadata = envelope.Metadata
    }, ct);

    return Results.Ok(new { status = "Continued", envelope.Metadata.OrchestrationInstanceId, envelope.Metadata.TaskExecutionId });
});

app.MapGet("/demo/instances/{instanceId}", async (string instanceId, RuntimeStorageDbContext db, CancellationToken ct) =>
{
    var id = new Id(Ulid.Parse(instanceId));
    var instance = await db.OrchestrationInstances.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    if (instance is null)
        return Results.NotFound();

    var artifactVersion = await db.Artifacts.AsNoTracking()
        .Where(x => x.Id == instance.RuntimeOrchestrationArtifactId)
        .Select(x => x.Version)
        .FirstOrDefaultAsync(ct);

    var stages = await db.StageExecutions.AsNoTracking()
        .Where(x => x.OrchestrationInstanceId == id)
        .OrderBy(x => x.Order)
        .Select(x => new { x.StageKey, x.Status, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc })
        .ToArrayAsync(ct);

    var tasks = await db.TaskExecutions.AsNoTracking()
        .Where(x => x.OrchestrationInstanceId == id)
        .OrderBy(x => x.StartedOnUtc)
        .Select(x => new { x.Id, x.StageExecutionId, x.TaskKey, x.TaskKind, x.Status, x.AwaitResponse, x.CorrelationId, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc, x.MetadataJson })
        .ToArrayAsync(ct);

    return Results.Ok(new
    {
        instance.Id,
        instance.OrchestrationDefinitionKey,
        ArtifactVersion = artifactVersion,
        instance.Status,
        instance.CurrentStageKey,
        instance.CurrentTaskKey,
        instance.StartedOnUtc,
        instance.CompletedOnUtc,
        instance.FailedOnUtc,
        stages,
        tasks
    });
});

app.MapGet("/demo/instances/by-correlation/{correlationId}", async (string correlationId, RuntimeStorageDbContext db, CancellationToken ct) =>
{
    var instance = await db.OrchestrationInstances.AsNoTracking()
        .Where(x => x.CorrelationId == correlationId)
        .OrderByDescending(x => x.StartedOnUtc)
        .FirstOrDefaultAsync(ct);

    if (instance is null)
        return Results.NotFound();

    var artifactVersion = await db.Artifacts.AsNoTracking()
        .Where(x => x.Id == instance.RuntimeOrchestrationArtifactId)
        .Select(x => x.Version)
        .FirstOrDefaultAsync(ct);

    var stages = await db.StageExecutions.AsNoTracking()
        .Where(x => x.OrchestrationInstanceId == instance.Id)
        .OrderBy(x => x.Order)
        .Select(x => new { x.StageKey, x.Status, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc })
        .ToArrayAsync(ct);

    var tasks = await db.TaskExecutions.AsNoTracking()
        .Where(x => x.OrchestrationInstanceId == instance.Id)
        .OrderBy(x => x.StartedOnUtc)
        .Select(x => new { x.Id, x.StageExecutionId, x.TaskKey, x.TaskKind, x.Status, x.AwaitResponse, x.CorrelationId, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc, x.MetadataJson })
        .ToArrayAsync(ct);

    var taskIds = tasks.Select(x => x.Id).ToArray();
    var attempts = await db.TaskExecutionAttempts.AsNoTracking()
        .Where(x => taskIds.Contains(x.TaskExecutionId))
        .OrderBy(x => x.StartedOnUtc)
        .Select(x => new { x.Id, x.TaskExecutionId, x.AttemptNumber, x.Status, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc, x.TimedOutOnUtc, x.RequestPayloadJson, x.ResponsePayloadJson, x.ErrorCode, x.ErrorMessage, x.MetadataJson })
        .ToArrayAsync(ct);

    var attemptIds = attempts.Select(x => x.Id).ToArray();
    var dispatches = await db.TaskDispatches.AsNoTracking()
        .Where(x => attemptIds.Contains(x.TaskExecutionAttemptId))
        .OrderBy(x => x.SentOnUtc)
        .Select(x => new { x.Id, x.TaskExecutionAttemptId, x.DispatchType, x.Destination, x.DispatchStatus, x.SentOnUtc, x.AcknowledgedOnUtc, x.FailedOnUtc, x.FailureReason, x.CorrelationId, x.RequestPayloadJson })
        .ToArrayAsync(ct);

    var compensations = await db.CompensationExecutions.AsNoTracking()
        .Where(x => x.OrchestrationInstanceId == instance.Id)
        .OrderBy(x => x.StartedOnUtc)
        .Select(x => new { x.Id, x.SourceTaskExecutionId, x.CompensationTaskKey, x.Status, x.StartedOnUtc, x.CompletedOnUtc, x.FailedOnUtc, x.RequestPayloadJson, x.ResponsePayloadJson, x.ErrorMessage, x.MetadataJson })
        .ToArrayAsync(ct);

    var transitions = await db.ExecutionTransitions.AsNoTracking()
        .Where(x => x.OrchestrationInstanceId == instance.Id)
        .OrderBy(x => x.OccurredOnUtc)
        .Select(x => new { x.Id, x.StageExecutionId, x.TaskExecutionId, x.TaskExecutionAttemptId, x.TransitionType, x.FromStatus, x.ToStatus, x.OccurredOnUtc, x.Message, x.PayloadJson })
        .ToArrayAsync(ct);

    return Results.Ok(new
    {
        instance.Id,
        instance.CorrelationId,
        instance.OrchestrationDefinitionKey,
        ArtifactVersion = artifactVersion,
        instance.Status,
        instance.CurrentStageKey,
        instance.CurrentTaskKey,
        instance.StartedOnUtc,
        instance.CompletedOnUtc,
        instance.FailedOnUtc,
        stages,
        tasks,
        attempts,
        dispatches,
        compensations,
        transitions
    });
});

app.MapGet("/demo/instances/by-correlation/{correlationId}/count", async (string correlationId, RuntimeStorageDbContext db, CancellationToken ct) =>
{
    var count = await db.OrchestrationInstances.AsNoTracking()
        .CountAsync(x => x.CorrelationId == correlationId, ct);
    return Results.Ok(new { correlationId, count });
});

app.MapGet("/demo/instances/by-correlation/{correlationId}/list", async (string correlationId, RuntimeStorageDbContext db, CancellationToken ct) =>
{
    var instances = await db.OrchestrationInstances.AsNoTracking()
        .Where(x => x.CorrelationId == correlationId)
        .OrderBy(x => x.StartedOnUtc)
        .Join(
            db.Artifacts.AsNoTracking(),
            instance => instance.RuntimeOrchestrationArtifactId,
            artifact => artifact.Id,
            (instance, artifact) => new
            {
                instance.Id,
                instance.CorrelationId,
                instance.OrchestrationDefinitionKey,
                ArtifactVersion = artifact.Version,
                instance.Status,
                instance.CurrentStageKey,
                instance.CurrentTaskKey,
                instance.StartedOnUtc,
                instance.CompletedOnUtc,
                instance.FailedOnUtc
            })
        .ToArrayAsync(ct);

    return Results.Ok(instances);
});

app.MapKrackendSagasOrchestrationsArtifactEndpoints();
app.MapKrackendSagasOrchestrationsEngineEndpoints();
app.MapOrchestratorRuntimeReactiveHub();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

public sealed class DemoOrderCreatedMessage
{
    public string MessageId { get; set; }
    public string OrderId { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string OrchestrationKey { get; set; }
    public string ArtifactVersion { get; set; }
}

public sealed class DemoOrchestrationEnvelope
{
    public string Topic { get; set; }
    public string Version { get; set; }
    public object Payload { get; set; }
    public OrchestratorMessageMetadata Metadata { get; set; }
}

public sealed class DemoBackChannelEnvelope
{
    public string Topic { get; set; }
    public string Version { get; set; }
    public object Payload { get; set; }
    public OrchestratorMessageMetadata Metadata { get; set; }
}

public sealed class DemoRabbitMessagePublisher : IMessagePublisher
{
    private static readonly ConcurrentDictionary<string, int> FlakyAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProducer _producer;
    private readonly IOrchestratorMetadataAccessor _metadataAccessor;
    private readonly IConfiguration _configuration;

    public DemoRabbitMessagePublisher(IProducer producer, IOrchestratorMetadataAccessor metadataAccessor, IConfiguration configuration)
    {
        _producer = producer;
        _metadataAccessor = metadataAccessor;
        _configuration = configuration;
    }

    public async Task<MessagePublishResult> Publish(MessagePublishRequest request, CancellationToken cancellationToken = default)
    {
        var metadata = _metadataAccessor.Current;
        if (string.Equals(request.Topic, "demo.dispatch-fail", StringComparison.OrdinalIgnoreCase))
        {
            return new MessagePublishResult
            {
                Succeeded = false,
                Status = "Failed",
                FailureReason = "Demo forced dispatch failure."
            };
        }

        var topic = request.Topic;
        if (string.Equals(request.Topic, "billing.charge-flaky", StringComparison.OrdinalIgnoreCase))
        {
            var key = metadata?.CorrelationId ?? Guid.NewGuid().ToString("N");
            var attempt = FlakyAttempts.AddOrUpdate(key, 1, (_, current) => current + 1);
            if (attempt == 1)
            {
                return new MessagePublishResult
                {
                    Succeeded = false,
                    Status = "Failed",
                    FailureReason = "Demo forced flaky dispatch failure."
                };
            }

            topic = "billing.charge";
        }

        var exchange = _configuration["RabbitMq:Exchange"];
        await _producer.PublishAsync(new DemoOrchestrationEnvelope
        {
            Topic = topic,
            Version = request.Version,
            Payload = request.Message,
            Metadata = metadata
        }, exchange, topic, Pigeon.Messaging.Contracts.SemanticVersion.Parse(request.Version), cancellationToken);

        return new MessagePublishResult
        {
            Succeeded = true,
            Status = "Published",
            ExternalReference = $"{exchange}:{topic}"
        };
    }
}

internal static class DemoRuntimeBackChannelRegistration
{
    public static void RegisterBackChannel(
        IPigeonServiceBuilder pigeon,
        string orchestrationKey,
        string rabbitQueueSuffix,
        IReadOnlyCollection<string> versions = null)
    {
        foreach (var version in versions ?? ["1.0.0", "2.0.0", "3.0.0", "4.0.0"])
        {
            var topic = BuildBackChannelTopic(orchestrationKey, version);
            pigeon.AddConsumeHandler<DemoBackChannelEnvelope>(
                topic,
                Pigeon.Messaging.Contracts.SemanticVersion.Parse(version),
                $"krackend.demo.runtime.backchannel.{orchestrationKey.Replace('.', '-')}.v{version.Replace('.', '-')}.{rabbitQueueSuffix}",
                async (context, envelope) =>
                {
                    var handler = context.Services.GetRequiredService<IRuntimeBackChannelResponseHandler>();
                    await handler.Handle(new MessageConsumeContext
                    {
                        Topic = envelope.Topic,
                        Version = envelope.Version,
                        CreatedOnUtc = DateTimeOffset.UtcNow,
                        Message = JsonSerializer.SerializeToNode(envelope.Payload),
                        Metadata = envelope.Metadata
                    }, context.CancellationToken);
                });
        }
    }

    private static string BuildBackChannelTopic(string orchestrationKey, string version)
    {
        var topic = $"orchestrations.{orchestrationKey}".Trim().Replace(" ", "_").ToLowerInvariant();
        var suffix = $".v{version.Trim().Replace(".", "-")}".ToLowerInvariant();
        return topic.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? topic
            : $"{topic}{suffix}";
    }
}

internal static class DemoStressMetrics
{
    public static double MaxAgeSeconds(IEnumerable<DateTimeOffset?> timestamps, DateTimeOffset now)
    {
        var oldest = timestamps
            .Where(x => x != null)
            .OrderBy(x => x)
            .FirstOrDefault();

        return oldest == null ? 0 : Math.Round((now - oldest.Value).TotalSeconds, 2);
    }

    public static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        if (percentile <= 0)
            return Math.Round(sortedValues[0], 2);

        if (percentile >= 100)
            return Math.Round(sortedValues[^1], 2);

        var rank = (percentile / 100d) * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return Math.Round(sortedValues[lower], 2);

        var weight = rank - lower;
        return Math.Round(sortedValues[lower] + ((sortedValues[upper] - sortedValues[lower]) * weight), 2);
    }
}
