namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

public sealed class RuntimeMessagingAcceptanceE2ETests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RuntimeDeploysArtifactStandsUpIngressAndCompletesThreeStageMessagingSaga()
    {
        var version = new SemanticVersion(1, 2, 0);
        var parallelGroupId = Id.New();
        using var harness = RuntimeAcceptanceHarness.Create();
        var artifact = CreateArtifact(
            "sales.sale.created",
            version,
            "acceptance-happy-path",
            [EventTrigger("events.sales.sale.created", version)],
            Stage("sale-created", 1, MessagingTask("sales.validate", 1, version)),
            StageWithGraph(
                "fulfillment",
                2,
                [new ParallelGroupArtifact(parallelGroupId, ParallelJoinPolicy.WaitAll, null)],
                MessagingTask("inventories.reserve", 1, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId),
                MessagingTask("payments.capture", 2, version, executionMode: TaskExecutionMode.Parallel, parallelGroupId: parallelGroupId)),
            Stage("confirmation", 3, MessagingTask("notifications.send", 1, version)));

        var deployment = await harness.DeployAsync(CreatePackage(artifact));

        Assert.True(deployment.Accepted, deployment.Message);
        var runtimeArtifact = await GetRuntimeArtifactAsync(harness, deployment.RuntimeArtifactId);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, runtimeArtifact.Status);
        Assert.Equal(2, harness.IngressAdapter.Connected.Count);
        Assert.Contains(harness.IngressAdapter.Connected, ingress =>
            ingress.IngressKind == IngressKind.Trigger &&
            ingress.Topic == "events.sales.sale.created" &&
            ingress.Version == "1.2.0");
        Assert.Contains(harness.IngressAdapter.Connected, ingress =>
            ingress.IngressKind == IngressKind.Backchannel &&
            ingress.Topic == "orchestrations.sales.sale.created" &&
            ingress.Version == "1.2.0");

        await harness.StartFromMessagingIngressAsync(
            "events.sales.sale.created",
            "1.2.0",
            BusinessPayload(("saleId", "S-100"), ("amount", "1200")),
            "sale-correlation-100");

        var saleValidation = Assert.Single(harness.Dispatcher.Commands);
        Assert.Equal("sales.validate", saleValidation.TaskKey);
        AssertBusinessPayloadWasNotWrapped(saleValidation.Payload);
        Assert.Contains("orchestrations.sales.sale.created", saleValidation.MessageMetadata.ReplyAddress!.SettingsPayload);

        await harness.ForwardAsync(
            saleValidation,
            BusinessPayload(("validated", "true")),
            Success("Sales", "Validate"));

        var reserveInventory = harness.Dispatcher.Commands.Single(command => command.TaskKey == "inventories.reserve");
        var capturePayment = harness.Dispatcher.Commands.Single(command => command.TaskKey == "payments.capture");
        Assert.Equal(3, harness.Dispatcher.Commands.Count);

        await harness.ForwardAsync(
            reserveInventory,
            BusinessPayload(("reserved", "true")),
            Success("Inventories", "Reserve"));

        Assert.Equal(3, harness.Dispatcher.Commands.Count);

        await harness.ForwardAsync(
            capturePayment,
            BusinessPayload(("captured", "true")),
            Success("Payments", "Capture"));

        var sendNotification = harness.Dispatcher.Commands.Single(command => command.TaskKey == "notifications.send");
        Assert.Equal(4, harness.Dispatcher.Commands.Count);

        await harness.ForwardAsync(
            sendNotification,
            BusinessPayload(("notified", "true")),
            Success("Notifications", "Send"));

        var instance = await GetInstanceAsync(harness, saleValidation);
        var stages = await GetStagesAsync(harness, saleValidation);
        var tasks = await GetTasksAsync(harness, saleValidation);
        var reserveAttempt = await GetAttemptAsync(harness, reserveInventory);
        var transitions = await GetTransitionsAsync(harness, saleValidation);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.All(stages, stage => Assert.Equal(StageExecutionStatus.Completed, stage.Status));
        Assert.All(tasks, task => Assert.Equal(TaskExecutionStatus.Completed, task.Status));
        Assert.Equal("true", reserveAttempt.ResponsePayload!["reserved"]!.GetValue<string>());
        Assert.False(reserveAttempt.ResponsePayload.AsObject().ContainsKey(nameof(OrchestrationExecutionResultMetadata.Succeeded)));
        Assert.True(reserveAttempt.Metadata["ExecutionSucceeded"]!.GetValue<bool>());
        Assert.Contains(transitions, transition => transition.TransitionType == "InstanceCompleted");
    }

    [Fact]
    public async Task RuntimeRetriesTransientFailureThenCompensatesWhenFinalFailureRequiresIt()
    {
        var version = new SemanticVersion(1, 0, 0);
        using var harness = RuntimeAcceptanceHarness.Create();
        var artifact = CreateArtifact(
            "sales.sale.created",
            version,
            "acceptance-retry-compensation",
            [EventTrigger("events.sales.sale.created", version)],
            Stage(
                "reservation",
                1,
                MessagingTask(
                    "inventories.reserve",
                    1,
                    version,
                    compensation: Compensation("inventories.release", version))),
            Stage(
                "payment",
                2,
                MessagingTask(
                    "payments.capture",
                    1,
                    version,
                    retryPolicy: RetryPolicy(1, "TransientPaymentFailure"),
                    onErrorPolicy: OnErrorPolicy.StopAndCompensate)));

        await harness.DeployAsync(CreatePackage(artifact));
        await harness.StartFromMessagingIngressAsync(
            "events.sales.sale.created",
            "1.0.0",
            BusinessPayload(("saleId", "S-200")),
            "sale-correlation-200");

        var reserveInventory = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(
            reserveInventory,
            BusinessPayload(("reserved", "true")),
            Success("Inventories", "Reserve"));

        var firstPayment = harness.Dispatcher.Commands.Single(command => command.TaskKey == "payments.capture");
        await harness.ForwardAsync(
            firstPayment,
            null,
            Failure("TransientPaymentFailure", "payment provider throttled"));

        var retryPayment = harness.Dispatcher.Commands.Last(command => command.TaskKey == "payments.capture");
        Assert.Equal(2, retryPayment.MessageMetadata.Attempt);

        await harness.ForwardAsync(
            retryPayment,
            null,
            Failure("PaymentRejected", "card rejected"));

        var instance = await GetInstanceAsync(harness, reserveInventory);
        var paymentAttempts = await GetAttemptsAsync(harness, firstPayment);
        var compensations = await GetCompensationsAsync(harness, reserveInventory);
        var compensationCommand = harness.Dispatcher.Commands.Last();

        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Equal(2, paymentAttempts.Count);
        Assert.Contains(paymentAttempts, attempt => attempt.ErrorCode == "TransientPaymentFailure");
        Assert.Contains(paymentAttempts, attempt => attempt.ErrorCode == "PaymentRejected");
        Assert.Single(compensations);
        Assert.Equal("inventories.reserve", compensationCommand.TaskKey);
        Assert.False(compensationCommand.AwaitResponse);
        Assert.Contains("inventories.release", compensationCommand.SettingsPayload);
    }

    [Fact]
    public async Task RuntimeReconcilesTimedOutTaskRetriesItAndCompletesWhenServiceEventuallyReplies()
    {
        var version = new SemanticVersion(1, 0, 0);
        using var harness = RuntimeAcceptanceHarness.Create();
        var artifact = CreateArtifact(
            "sales.sale.created",
            version,
            "acceptance-timeout-reconcile",
            [EventTrigger("events.sales.sale.created", version)],
            Stage(
                "risk",
                1,
                MessagingTask(
                    "risk.evaluate",
                    1,
                    version,
                    timeoutPolicy: ReconcileTimeoutPolicy(
                        Duration.FromSeconds(1),
                        RetryPolicy(1, "TaskTimedOut")))));

        await harness.DeployAsync(CreatePackage(artifact));
        await harness.StartFromMessagingIngressAsync(
            "events.sales.sale.created",
            "1.0.0",
            BusinessPayload(("saleId", "S-300")),
            "sale-correlation-300");

        var firstRiskEvaluation = harness.Dispatcher.Commands.Single();
        var processed = await harness
            .GetRequiredService<IOrchestrationTimeoutProcessor>()
            .ProcessDueTimeoutsAsync(DateTime.UtcNow.AddSeconds(2));

        Assert.Equal(1, processed);
        var retryRiskEvaluation = harness.Dispatcher.Commands.Last();
        Assert.Equal("risk.evaluate", retryRiskEvaluation.TaskKey);
        Assert.Equal(2, retryRiskEvaluation.MessageMetadata.Attempt);

        await harness.ForwardAsync(
            retryRiskEvaluation,
            BusinessPayload(("approved", "true")),
            Success("Risk", "Evaluate"));

        var instance = await GetInstanceAsync(harness, firstRiskEvaluation);
        var attempts = await GetAttemptsAsync(harness, firstRiskEvaluation);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Contains(attempts, attempt => attempt.Status == TaskExecutionStatus.TimedOut);
        Assert.Contains(attempts, attempt => attempt.Status == TaskExecutionStatus.Completed);
    }

    [Fact]
    public async Task RuntimeKeepsConcurrentArtifactVersionsSelectableByMessagingTopicAndVersion()
    {
        using var harness = RuntimeAcceptanceHarness.Create();
        var versionOne = new SemanticVersion(1, 0, 0);
        var versionTwo = new SemanticVersion(1, 1, 0);
        var artifactOne = CreateArtifact(
            "sales.sale.created",
            versionOne,
            "acceptance-version-one",
            [EventTrigger("events.sales.sale.created", versionOne)],
            Stage("single", 1, MessagingTask("sales.v1.process", 1, versionOne)));
        var artifactTwo = CreateArtifact(
            "sales.sale.created",
            versionTwo,
            "acceptance-version-two",
            [EventTrigger("events.sales.sale.created.v1_1", versionTwo)],
            Stage("single", 1, MessagingTask("sales.v1_1.process", 1, versionTwo)));

        var firstDeployment = await harness.DeployAsync(CreatePackage(artifactOne));
        var secondDeployment = await harness.DeployAsync(CreatePackage(artifactTwo));

        Assert.True(firstDeployment.Accepted, firstDeployment.Message);
        Assert.True(secondDeployment.Accepted, secondDeployment.Message);

        await harness.StartFromMessagingIngressAsync(
            "events.sales.sale.created",
            "1.0.0",
            BusinessPayload(("saleId", "S-400")),
            "sale-correlation-400");
        await harness.StartFromMessagingIngressAsync(
            "events.sales.sale.created.v1_1",
            "1.1.0",
            BusinessPayload(("saleId", "S-401")),
            "sale-correlation-401");

        var v1Command = harness.Dispatcher.Commands.Single(command => command.TaskKey == "sales.v1.process");
        var v11Command = harness.Dispatcher.Commands.Single(command => command.TaskKey == "sales.v1_1.process");
        var v1Instance = await GetInstanceAsync(harness, v1Command);
        var v11Instance = await GetInstanceAsync(harness, v11Command);

        Assert.NotEqual(v1Instance.RuntimeOrchestrationArtifactId, v11Instance.RuntimeOrchestrationArtifactId);
        Assert.Equal(firstDeployment.RuntimeArtifactId, v1Instance.RuntimeOrchestrationArtifactId.ToString());
        Assert.Equal(secondDeployment.RuntimeArtifactId, v11Instance.RuntimeOrchestrationArtifactId.ToString());
        Assert.Contains(harness.IngressAdapter.Connected, ingress =>
            ingress.Topic == "events.sales.sale.created" && ingress.Version == "1.0.0");
        Assert.Contains(harness.IngressAdapter.Connected, ingress =>
            ingress.Topic == "events.sales.sale.created.v1_1" && ingress.Version == "1.1.0");
    }

    private static RuntimeArtifactDeliveryPackage CreatePackage(OrchestrationArtifact artifact)
        => new()
        {
            ReleaseTargetId = Id.New().ToString(),
            ArtifactId = Id.New().ToString(),
            ArtifactType = "orchestration-version-snapshot",
            SchemaVersion = "1.0",
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId.ToString(),
            OrchestrationVersionId = artifact.OrchestrationVersionId.ToString(),
            OrchestrationDefinitionKey = artifact.Key,
            Version = artifact.Version.ToString(),
            Checksum = artifact.Checksum.Value,
            PayloadJson = JsonSerializer.Serialize(artifact, SerializerOptions),
            CorrelationId = Id.New().ToString(),
            PromotedBy = "acceptance-tests",
            PromotedOnUtc = DateTime.UtcNow
        };

    private static OrchestrationArtifact CreateArtifact(
        string key,
        SemanticVersion version,
        string checksum,
        IReadOnlyList<TriggerBindingArtifact> triggers,
        params StageArtifact[] stages)
        => new(
            Id.New(),
            Id.New(),
            key,
            key,
            "sales",
            version,
            new Checksum(checksum),
            triggers,
            [],
            stages);

    private static TriggerBindingArtifact EventTrigger(string topic, SemanticVersion version)
    {
        var schemaBinding = new SchemaBindingArtifact(
            Id.New(),
            ElementType.Orchestration,
            Id.New(),
            Id.New(),
            topic,
            version,
            Id.New(),
            false)
        {
            ContractKind = SchemaContractKind.Event
        };

        return new TriggerBindingArtifact(
            Id.New(),
            TriggerType.Event,
            new EventTriggerChannelArtifact(schemaBinding, topic, version),
            true,
            $"Trigger {topic}");
    }

    private static StageArtifact Stage(string key, int order, params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, DisabledCondition(), tasks, [], []);

    private static StageArtifact StageWithGraph(
        string key,
        int order,
        IReadOnlyList<ParallelGroupArtifact> parallelGroups,
        params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, DisabledCondition(), tasks, parallelGroups, []);

    private static TaskArtifact MessagingTask(
        string key,
        int order,
        SemanticVersion version,
        RetryPolicyArtifact? retryPolicy = null,
        TimeoutPolicyArtifact? timeoutPolicy = null,
        OnErrorPolicy onErrorPolicy = OnErrorPolicy.Stop,
        CompensationArtifact? compensation = null,
        TaskExecutionMode executionMode = TaskExecutionMode.Sequential,
        Id? parallelGroupId = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Messaging,
            executionMode,
            parallelGroupId,
            DisabledCondition(),
            DisabledTransformation(),
            new MessagingTaskConfigurationArtifact(key, version, null!),
            retryPolicy,
            timeoutPolicy,
            onErrorPolicy,
            compensation,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static CompensationArtifact Compensation(string topic, SemanticVersion version)
        => new(
            TaskKind.Messaging,
            DisabledTransformation(),
            DisabledCondition(),
            new MessagingTaskConfigurationArtifact(topic, version, null!),
            null,
            null,
            TaskDispatchType.FireAndForget);

    private static RetryPolicyArtifact RetryPolicy(int maxRetries, params string[] retryableErrorCodes)
        => new(
            maxRetries,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            retryableErrorCodes,
            true);

    private static TimeoutPolicyArtifact ReconcileTimeoutPolicy(Duration timeout, RetryPolicyArtifact retryPolicy)
        => new(
            timeout,
            TimeoutBehavior.Reconcile,
            new ReconcileTimeoutBehaviorPolicyArtifact(OrchestrationActionOnTimeout.Block, retryPolicy));

    private static ExecutionConditionArtifact DisabledCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(string.Empty)))
        {
            IsEnabled = false
        };

    private static TransformationArtifact DisabledTransformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact())
        {
            IsEnabled = false
        };

    private static OrchestrationExecutionResultMetadata Success(string serviceName, string operationName)
        => new()
        {
            Succeeded = true,
            Status = "Succeeded",
            ServiceName = serviceName,
            OperationName = operationName,
            CompletedOnUtc = DateTime.UtcNow
        };

    private static OrchestrationExecutionResultMetadata Failure(string errorCode, string errorMessage)
        => new()
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CompletedOnUtc = DateTime.UtcNow
        };

    private static JsonNode BusinessPayload(params (string Name, string Value)[] values)
    {
        var payload = new JsonObject();
        foreach (var (name, value) in values)
        {
            payload[name] = value;
        }

        return payload;
    }

    private static void AssertBusinessPayloadWasNotWrapped(string payload)
    {
        var json = JsonNode.Parse(payload)!.AsObject();
        Assert.False(json.ContainsKey(nameof(OrchestrationExecutionResultMetadata.Succeeded)));
        Assert.False(json.ContainsKey(nameof(OrchestrationExecutionResultMetadata.Status)));
        Assert.False(json.ContainsKey(nameof(OrchestrationExecutionResultMetadata.Metadata)));
    }

    private static async Task<RuntimeOrchestrationArtifact> GetRuntimeArtifactAsync(
        RuntimeAcceptanceHarness harness,
        string artifactId)
        => await harness.GetRequiredService<IRuntimeArtifactRepository>().GetById(ParseId(artifactId));

    private static async Task<OrchestrationInstance> GetInstanceAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => await harness.GetRequiredService<IOrchestrationInstanceRepository>().GetById(ParseId(command.OrchestrationInstanceId));

    private static async Task<TaskExecutionAttempt> GetAttemptAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => await harness.GetRequiredService<ITaskExecutionAttemptRepository>().GetById(ParseId(command.TaskExecutionAttemptId));

    private static Task<IReadOnlyCollection<TaskExecutionAttempt>> GetAttemptsAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => harness.GetRequiredService<ITaskExecutionAttemptRepository>().GetByTaskExecutionId(ParseId(command.TaskExecutionId));

    private static Task<IReadOnlyCollection<TaskExecution>> GetTasksAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => harness.GetRequiredService<ITaskExecutionRepository>().GetByInstanceId(ParseId(command.OrchestrationInstanceId));

    private static Task<IReadOnlyCollection<StageExecution>> GetStagesAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => harness.GetRequiredService<IStageExecutionRepository>().GetByInstanceId(ParseId(command.OrchestrationInstanceId));

    private static Task<IReadOnlyCollection<CompensationExecution>> GetCompensationsAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => harness.GetRequiredService<ICompensationExecutionRepository>().GetByInstanceId(ParseId(command.OrchestrationInstanceId));

    private static Task<IReadOnlyCollection<ExecutionTransition>> GetTransitionsAsync(
        RuntimeAcceptanceHarness harness,
        RemoteCommand command)
        => harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(ParseId(command.OrchestrationInstanceId));

    private static Id ParseId(string value)
        => new(Ulid.Parse(value));
}
