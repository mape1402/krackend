using System.Text.Json;
using Krackend.Sagas.Orchestrations.ControlPlaneHost.Sample.Bootstrap;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

const string adminRootPath = "admin";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

var sqlConnection = builder.Configuration.GetConnectionString("ControlPlaneDocker")
    ?? builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(sqlConnection))
    throw new InvalidOperationException("Set ConnectionStrings:ControlPlaneDocker or ConnectionStrings:Default for the Design host.");

var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddHealthChecks()
    .AddAsyncCheck("sql", async () =>
    {
        await using var connection = new SqlConnection(sqlConnection);
        await connection.OpenAsync();
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
    }, tags: ["ready"]);

builder.Services.AddOrchestratorControlPlane(options =>
{
    options.AdminRootPath = adminRootPath;
    options.ConfigureStorage = db => db.UseSqlServer(
        sqlConnection,
        sql => sql.MigrationsAssembly(migrationsAssembly));
});
builder.Services.AddScoped<IDesignHostSeedDataSeeder, DesignHostSeedDataSeeder>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();

    await dbContext.Database.MigrateAsync();

    var seedDataSeeder = scope.ServiceProvider.GetRequiredService<IDesignHostSeedDataSeeder>();
    await seedDataSeeder.SeedAsync();
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapGet("/", context =>
{
    context.Response.Redirect($"/{adminRootPath}");
    return Task.CompletedTask;
});

app.MapGet("/demo/artifacts", () => Results.Ok(DemoArtifactFactory.List()));
app.MapGet("/demo/artifact/order-fulfillment", () => Results.Ok(DemoArtifactFactory.Create("order-fulfillment", "1.0.0")));
app.MapGet("/demo/artifact/{artifactKey}", (string artifactKey) => Results.Ok(DemoArtifactFactory.Create(artifactKey, null)));
app.MapGet("/demo/artifact/{artifactKey}/{version}", (string artifactKey, string version) => Results.Ok(DemoArtifactFactory.Create(artifactKey, version)));
app.MapGet("/demo/health", () => Results.Ok(new
{
    service = "KrackendDemo.DesignHost",
    artifact = DemoArtifactFactory.OrchestrationKey,
    versions = DemoArtifactFactory.List()
}));

app.MapOrchestratorArtifactDeliveryEndpoints();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

internal static class DemoArtifactFactory
{
    public const string OrchestrationKey = "order.fulfillment";
    public const string ArtifactVersion = "1.0.0";
    private const string ParallelGroupId = "01K00000000000000000000001";

    public static object[] List()
        =>
        [
            new { ArtifactKey = "order-fulfillment", OrchestrationKey, Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-fulfillment", OrchestrationKey, Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-approval", OrchestrationKey = "order.approval", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-approval", OrchestrationKey = "order.approval", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-conditions", OrchestrationKey = "order.conditions", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-parallel-fulfillment", OrchestrationKey = "order.parallel-fulfillment", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-parallel-fulfillment", OrchestrationKey = "order.parallel-fulfillment", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-retry-payment", OrchestrationKey = "order.retry-payment", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-retry-payment", OrchestrationKey = "order.retry-payment", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-error-policy", OrchestrationKey = "order.error-policy", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-error-policy", OrchestrationKey = "order.error-policy", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-error-policy", OrchestrationKey = "order.error-policy", Version = "3.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-error-policy", OrchestrationKey = "order.error-policy", Version = "4.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-timeout", OrchestrationKey = "order.timeout", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-timeout", OrchestrationKey = "order.timeout", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-transform-payload", OrchestrationKey = "order.transform-payload", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-fire-and-forget", OrchestrationKey = "order.fire-and-forget", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-unsupported-transport", OrchestrationKey = "order.unsupported-transport", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-unsupported-transport", OrchestrationKey = "order.unsupported-transport", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-unsupported-transport", OrchestrationKey = "order.unsupported-transport", Version = "3.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "1.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "2.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "3.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "4.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "5.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "6.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "7.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-structure", OrchestrationKey = "order.structure", Version = "8.0.0", TriggerTopic = "orders.created" },
            new { ArtifactKey = "order-design-only", OrchestrationKey = "order.design-only", Version = "1.0.0", TriggerTopic = "orders.created" }
        ];

    public static object Create(string artifactKey, string version)
    {
        var artifactVersion = string.IsNullOrWhiteSpace(version) ? ArtifactVersion : version.Trim();
        var key = ResolveOrchestrationKey(artifactKey);
        var payload = new
        {
            Key = key,
            Version = artifactVersion,
            Stages = CreateStages(artifactKey, artifactVersion)
        };

        return new
        {
            ArtifactId = Ulid.NewUlid().ToString(),
            ArtifactType = "orchestration.deploy",
            SchemaVersion = "1.0.0",
            EnvironmentKey = "local",
            OrchestrationDefinitionId = Ulid.NewUlid().ToString(),
            OrchestrationVersionId = Ulid.NewUlid().ToString(),
            OrchestrationDefinitionKey = key,
            Version = artifactVersion,
            Checksum = $"demo-{artifactKey}-{artifactVersion.Replace(".", "-")}",
            PayloadJson = JsonSerializer.Serialize(payload),
            CorrelationId = $"demo-{Ulid.NewUlid()}",
            PromotedBy = "KrackendDemo.DesignHost",
            PromotedOnUtc = DateTime.UtcNow
        };
    }

    private static string ResolveOrchestrationKey(string artifactKey)
        => artifactKey.ToLowerInvariant() switch
        {
            "order-fulfillment" => "order.fulfillment",
            "order-approval" => "order.approval",
            "order-conditions" => "order.conditions",
            "order-parallel-fulfillment" => "order.parallel-fulfillment",
            "order-retry-payment" => "order.retry-payment",
            "order-error-policy" => "order.error-policy",
            "order-timeout" => "order.timeout",
            "order-transform-payload" => "order.transform-payload",
            "order-fire-and-forget" => "order.fire-and-forget",
            "order-unsupported-transport" => "order.unsupported-transport",
            "order-structure" => "order.structure",
            "order-design-only" => "order.design-only",
            _ => throw new InvalidOperationException($"Demo artifact '{artifactKey}' is not registered.")
        };

    private static object[] CreateStages(string artifactKey, string artifactVersion)
        => artifactKey.ToLowerInvariant() switch
        {
            "order-fulfillment" => artifactVersion == "2.0.0" ? FulfillmentV2Stages() : FulfillmentV1Stages(),
            "order-approval" => ApprovalStages(artifactVersion),
            "order-conditions" => ConditionsStages(),
            "order-parallel-fulfillment" => ParallelStages(artifactVersion),
            "order-retry-payment" => RetryStages(artifactVersion),
            "order-error-policy" => ErrorPolicyStages(artifactVersion),
            "order-timeout" => TimeoutStages(artifactVersion),
            "order-transform-payload" => TransformStages(),
            "order-fire-and-forget" => FireAndForgetStages(),
            "order-unsupported-transport" => UnsupportedStages(artifactVersion),
            "order-structure" => StructureStages(artifactVersion),
            "order-design-only" => FulfillmentV1Stages(),
            _ => throw new InvalidOperationException($"Demo artifact '{artifactKey}' is not registered.")
        };

    private static object[] FulfillmentV1Stages()
        =>
        [
            new { Key = "reserve-inventory", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 1, "inventory.reserve", "1.0.0") } },
            new { Key = "charge-payment", Order = 2, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } },
            new { Key = "confirm-inventory", Order = 3, Tasks = new[] { MessagingTask("confirm-stock", 1, "inventory.confirm", "1.0.0") } }
        ];

    private static object[] FulfillmentV2Stages()
        =>
        [
            new { Key = "reserve-inventory", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 1, "inventory.reserve", "1.0.0") } },
            new { Key = "risk-review", Order = 2, Tasks = new[] { MessagingTask("score-risk", 1, "risk.score", "1.0.0") } },
            new { Key = "charge-payment", Order = 3, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } },
            new { Key = "confirm-inventory", Order = 4, Tasks = new[] { MessagingTask("confirm-stock", 1, "inventory.confirm", "1.0.0") } },
            new { Key = "schedule-shipping", Order = 5, Tasks = new[] { MessagingTask("schedule-shipping", 1, "shipping.schedule", "1.0.0") } }
        ];

    private static object[] ApprovalStages(string version)
    {
        var riskStageId = "risk-stage";
        var chargeStageId = "charge-stage";
        var rejectStageId = "reject-stage";
        return
        [
            new
            {
                Id = riskStageId,
                Key = "risk-review",
                Order = 1,
                BranchRules = version == "2.0.0"
                    ? new[] { BranchRule("approval-rejected", riskStageId, rejectStageId, "true") }
                    : new[] { BranchRule("approval-approved", riskStageId, chargeStageId, "false") },
                Tasks = new[] { MessagingTask("score-risk", 1, "risk.score", "1.0.0") }
            },
            new { Id = chargeStageId, Key = "charge-payment", Order = 2, ExecutionCondition = version == "2.0.0" ? Condition("false") : null, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } },
            new { Id = rejectStageId, Key = version == "2.0.0" ? "reject-order" : "schedule-shipping", Order = 3, Tasks = new[] { MessagingTask(version == "2.0.0" ? "reject-order" : "schedule-shipping", 1, version == "2.0.0" ? "notification.reject-order" : "shipping.schedule", "1.0.0") } }
        ];
    }

    private static object[] ConditionsStages()
        =>
        [
            new { Key = "skip-stage", Order = 1, ExecutionCondition = Condition("false"), Tasks = new[] { MessagingTask("should-not-run", 1, "demo.dispatch-fail", "1.0.0") } },
            new { Key = "task-conditions", Order = 2, Tasks = new[] { MessagingTask("skip-task", 1, "demo.dispatch-fail", "1.0.0", executionCondition: Condition("false")), MessagingTask("charge-payment", 2, "billing.charge", "1.0.0") } }
        ];

    private static object[] ParallelStages(string version)
    {
        var parallelTasks = version == "2.0.0"
            ? new[]
            {
                MessagingTask("fraud-screen", 1, "risk.fraud-screen", "1.0.0", executionMode: "Parallel", parallelGroupId: ParallelGroupId),
                MessagingTask("tax-calculate", 2, "billing.fail", "1.0.0", executionMode: "Parallel", parallelGroupId: ParallelGroupId),
                MessagingTask("shipping-quote", 3, "shipping.quote", "1.0.0", executionMode: "Parallel", parallelGroupId: ParallelGroupId)
            }
            : new[]
            {
                MessagingTask("fraud-screen", 1, "risk.fraud-screen", "1.0.0", executionMode: "Parallel", parallelGroupId: ParallelGroupId),
                MessagingTask("tax-calculate", 2, "billing.tax-calculate", "1.0.0", executionMode: "Parallel", parallelGroupId: ParallelGroupId),
                MessagingTask("shipping-quote", 3, "shipping.quote", "1.0.0", executionMode: "Parallel", parallelGroupId: ParallelGroupId)
            };

        return
        [
            new { Key = "reserve-inventory", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 1, "inventory.reserve", "1.0.0") } },
            new
            {
                Key = "parallel-work",
                Order = 2,
                ParallelGroups = new[] { new { Id = ParallelGroupId, Key = "parallel-checks", MaxParallelAgents = 3 } },
                Tasks = parallelTasks
            },
            new { Key = "charge-payment", Order = 3, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
        ];
    }

    private static object[] RetryStages(string version)
        => version == "2.0.0"
            ?
            [
                new { Key = "charge-payment", Order = 1, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.fail", "1.0.0", retryPolicy: RetryPolicy(1)) } },
                new { Key = "confirm-inventory", Order = 2, Tasks = new[] { MessagingTask("confirm-stock", 1, "inventory.confirm", "1.0.0") } }
            ]
            :
            [
                new { Key = "charge-payment", Order = 1, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge-fail-once", "1.0.0", retryPolicy: RetryPolicy(1)) } },
                new { Key = "confirm-inventory", Order = 2, Tasks = new[] { MessagingTask("confirm-stock", 1, "inventory.confirm", "1.0.0") } }
            ];

    private static object[] ErrorPolicyStages(string version)
        => version switch
        {
            "1.0.0" =>
            [
                new { Key = "optional-failure", Order = 1, Tasks = new[] { MessagingTask("optional-failure", 1, "billing.fail", "1.0.0", onErrorPolicy: "Continue") } },
                new { Key = "charge-payment", Order = 2, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
            ],
            "2.0.0" =>
            [
                new { Key = "stop-failure", Order = 1, Tasks = new[] { MessagingTask("stop-failure", 1, "billing.fail", "1.0.0", onErrorPolicy: "Stop") } },
                new { Key = "should-not-run", Order = 2, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
            ],
            "4.0.0" =>
            [
                new { Key = "reserve-inventory", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 1, "inventory.reserve", "1.0.0", compensation: Compensation("inventory.release")) } },
                new { Key = "charge-payment", Order = 2, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0", compensation: Compensation("inventory.release")) } },
                new { Key = "compensating-failure", Order = 3, Tasks = new[] { MessagingTask("compensating-failure", 1, "billing.fail", "1.0.0", onErrorPolicy: "StopAndCompensate") } },
                new { Key = "should-not-run", Order = 4, Tasks = new[] { MessagingTask("confirm-stock", 1, "inventory.confirm", "1.0.0") } }
            ],
            _ =>
            [
                new { Key = "reserve-inventory", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 1, "inventory.reserve", "1.0.0", compensation: Compensation("inventory.release")) } },
                new { Key = "compensating-failure", Order = 2, Tasks = new[] { MessagingTask("compensating-failure", 1, "billing.fail", "1.0.0", onErrorPolicy: "StopAndCompensate") } },
                new { Key = "should-not-run", Order = 3, Tasks = new[] { MessagingTask("confirm-stock", 1, "inventory.confirm", "1.0.0") } }
            ]
        };

    private static object[] TimeoutStages(string version)
        =>
        [
            new { Key = "shipping-timeout", Order = 1, Tasks = new[] { MessagingTask("shipping-never-responds", 1, "shipping.never-respond", "1.0.0", timeoutPolicy: TimeoutPolicy(version == "2.0.0" || version == "3.0.0" ? "Reconcile" : "Fail", version == "3.0.0" ? 120 : 1)) } }
        ];

    private static object[] TransformStages()
        =>
        [
            new { Key = "transformed-charge", Order = 1, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0", transformation: new { IsEnabled = true, Engine = "DSL", Configuration = new { Expression = new { Value = "identity" } } }) } }
        ];

    private static object[] FireAndForgetStages()
        =>
        [
            new { Key = "audit", Order = 1, Tasks = new[] { MessagingTask("audit-order", 1, "notification.audit", "1.0.0", dispatchType: "FireAndForget") } },
            new { Key = "charge-payment", Order = 2, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
        ];

    private static object[] UnsupportedStages(string version)
    {
        var kind = version switch
        {
            "2.0.0" => "Plugin",
            "3.0.0" => "HumanApproval",
            _ => "Http"
        };

        return
        [
            new { Key = $"unsupported-{kind.ToLowerInvariant()}", Order = 1, Tasks = new[] { UnsupportedTask($"{kind.ToLowerInvariant()}-call", 1, kind) } }
        ];
    }

    private static object[] UnsupportedStages()
        =>
        [
            new { Key = "unsupported-http", Order = 1, Tasks = new[] { UnsupportedTask("http-call", 1, "Http") } }
        ];

    private static object[] StructureStages(string version)
        => version switch
        {
            "1.0.0" =>
            [
                new { Key = "empty-stage", Order = 1, Tasks = Array.Empty<object>() }
            ],
            "2.0.0" =>
            [
                new { Key = "task-disabled", Order = 1, Tasks = new[] { MessagingTask("disabled-task", 1, "demo.dispatch-fail", "1.0.0", isEnabled: false), MessagingTask("charge-payment", 2, "billing.charge", "1.0.0") } }
            ],
            "3.0.0" =>
            [
                new { Key = "stage-disabled-by-condition", Order = 1, ExecutionCondition = Condition("false"), Tasks = new[] { MessagingTask("should-not-dispatch", 1, "demo.dispatch-fail", "1.0.0") } },
                new { Key = "charge-payment", Order = 2, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
            ],
            "4.0.0" =>
            [
                new { Key = "task-order-gaps", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 10, "inventory.reserve", "1.0.0"), MessagingTask("charge-payment", 30, "billing.charge", "1.0.0"), MessagingTask("confirm-stock", 50, "inventory.confirm", "1.0.0") } }
            ],
            "5.0.0" =>
            [
                new { Key = "duplicate-order-a", Order = 1, Tasks = new[] { MessagingTask("reserve-stock", 1, "inventory.reserve", "1.0.0") } },
                new { Key = "duplicate-order-b", Order = 1, Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
            ],
            "6.0.0" =>
            [
                new { Key = "duplicate-task-order", Order = 1, Tasks = new[] { MessagingTask("duplicate-order-a", 1, "inventory.reserve", "1.0.0"), MessagingTask("duplicate-order-b", 1, "billing.charge", "1.0.0") } }
            ],
            "7.0.0" =>
            [
                new { Key = "condition-true", Order = 1, ExecutionCondition = Condition("true"), Tasks = new[] { MessagingTask("charge-payment", 1, "billing.charge", "1.0.0") } }
            ],
            _ =>
            [
                new { Key = "condition-false", Order = 1, ExecutionCondition = Condition("false"), Tasks = new[] { MessagingTask("should-not-dispatch", 1, "demo.dispatch-fail", "1.0.0") } }
            ]
        };

    private static object MessagingTask(
        string key,
        int order,
        string topic,
        string version,
        string executionMode = "Sequential",
        string parallelGroupId = "",
        string onErrorPolicy = "Stop",
        string dispatchType = "FireAndWaitCallback",
        object executionCondition = null,
        object transformation = null,
        object retryPolicy = null,
        object timeoutPolicy = null,
        object compensation = null,
        bool isEnabled = true)
        => new
        {
            Key = key,
            Order = order,
            Kind = "Messaging",
            ExecutionMode = executionMode,
            DispatchType = dispatchType,
            ParallelGroupId = parallelGroupId,
            OnErrorPolicy = onErrorPolicy,
            IsEnabled = isEnabled,
            ExecutionCondition = executionCondition,
            Transformation = transformation,
            RetryPolicy = retryPolicy,
            TimeoutPolicy = timeoutPolicy,
            Compensation = compensation,
            Configuration = new { Topic = topic, Version = version }
        };

    private static object UnsupportedTask(string key, int order, string kind)
        => new
        {
            Key = key,
            Order = order,
            Kind = kind,
            ExecutionMode = "Sequential",
            DispatchType = "FireAndWaitCallback",
            OnErrorPolicy = "Stop",
            IsEnabled = true,
            Configuration = new { Topic = "unsupported.http", Version = "1.0.0" }
        };

    private static object BranchRule(string id, string fromId, string navigateToId, string expression)
        => new
        {
            Id = id,
            FromType = "Stage",
            FromId = fromId,
            NavigateToType = "Stage",
            NavigateToId = navigateToId,
            Condition = Condition(expression)
        };

    private static object Condition(string expression)
        => new { IsEnabled = true, Engine = "DSL", Configuration = new { Expression = new { Value = expression } } };

    private static object RetryPolicy(int maxRetries)
        => new { MaxRetries = maxRetries, StrategyType = "Fixed" };

    private static object TimeoutPolicy(string behavior, int timeoutSeconds = 1)
        => new { Timeout = timeoutSeconds, TimeoutBehavior = behavior, TimeoutBehaviorPolicy = new { OrchestrationAction = "Block", ErrorCode = $"Demo{behavior}Timeout" } };

    private static object Compensation(string topic)
        => new { CompensationTaskKind = "Messaging", DispatchType = "FireAndForget", Configuration = new { Topic = topic, Version = "1.0.0" } };
}
