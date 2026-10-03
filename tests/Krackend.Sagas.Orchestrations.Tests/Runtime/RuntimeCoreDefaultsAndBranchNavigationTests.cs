namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Execution;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public sealed class RuntimeCoreDefaultsAndBranchNavigationTests
{
    [Fact]
    public async Task DefaultIngressAccessorsAndMessagingAdapterAreNoopFallbacks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var allAccessor = scope.ServiceProvider.GetRequiredService<IGetAllIngressConfigurationsAccessor>();
        var artifactAccessor = scope.ServiceProvider.GetRequiredService<IGetIngressConfigurationByArtifactAccessor>();
        var messagingAdapter = scope.ServiceProvider.GetRequiredService<IMessagingIngressAdapter>();

        var all = await allAccessor.ReadAsync();
        var byArtifact = await artifactAccessor.GetConfigurationAsync(Id.New().ToString());
        await messagingAdapter.ConnectAsync(new MessagingConfiguration
        {
            ArtifactId = Id.New().ToString(),
            Topic = "events.sales.sale.created",
            Version = "1.0.0"
        });
        await messagingAdapter.DisconnectAsync("connector-1");
        allAccessor.Dispose();

        Assert.False(all.HasMoreItems);
        Assert.Empty(all.Configurations);
        Assert.Empty(byArtifact);
    }

    [Fact]
    public void ScopedMetadataAccessorsStoreAndClearOrchestrationMetadata()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var messageAccessor = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataAccessor>();
        var messageSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationMessageMetadataSetter>();
        var resultAccessor = scope.ServiceProvider.GetRequiredService<IOrchestrationExecutionResultMetadataAccessor>();
        var resultSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationExecutionResultMetadataSetter>();
        var propagationAccessor = scope.ServiceProvider.GetRequiredService<IOrchestrationPropagationMetadataAccessor>();
        var propagationSetter = scope.ServiceProvider.GetRequiredService<IOrchestrationPropagationMetadataSetter>();
        var messageMetadata = new OrchestrationMessageMetadata { SagaId = "saga-1" };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded",
            OperationName = "reserve"
        };
        var propagationMetadata = new OrchestrationPropagationMetadata();
        propagationMetadata.Items["tenant"] = JsonNode.Parse("""{"id":"north"}""")!;

        messageSetter.Set(messageMetadata);
        resultSetter.Set(resultMetadata);
        propagationSetter.Set(propagationMetadata);
        propagationMetadata.Items["tenant"]!["id"] = "changed";

        Assert.Same(messageMetadata, messageAccessor.Get());
        Assert.Same(resultMetadata, resultAccessor.Get());
        Assert.Equal("north", propagationAccessor.Get().Items["tenant"]!["id"]!.GetValue<string>());

        messageSetter.Set(null!);
        resultSetter.Clear();
        propagationSetter.Set(null!);

        Assert.NotNull(messageAccessor.Get());
        Assert.Null(messageAccessor.Get().SagaId);
        Assert.Null(resultAccessor.Get());
        Assert.Empty(propagationAccessor.Get().Items);
        propagationSetter.Set(new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["trace"] = JsonValue.Create("trace-1")!
            }
        });
        propagationSetter.Clear();
        Assert.Empty(propagationAccessor.Get().Items);
    }

    [Fact]
    public async Task InMemoryRuntimeRepositoriesReturnMissingValuesAndTrafficForRecentTerminalInstances()
    {
        var now = DateTime.UtcNow;
        var instanceId = Id.New();
        var artifactId = Id.New();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var instances = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var stages = scope.ServiceProvider.GetRequiredService<IStageExecutionRepository>();
        var compensations = scope.ServiceProvider.GetRequiredService<ICompensationExecutionRepository>();
        var transitions = scope.ServiceProvider.GetRequiredService<IExecutionTransitionRepository>();

        await instances.Create(new OrchestrationInstance
        {
            Id = instanceId,
            RuntimeOrchestrationArtifactId = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation",
            SagaId = "saga",
            ExecutionKey = "sales.sale.created:correlation",
            Status = OrchestrationInstanceStatus.Completed,
            StartedOnUtc = now.AddHours(-4),
            CompletedOnUtc = now.AddMinutes(-15),
            LastUpdatedOnUtc = now.AddMinutes(-15)
        });

        var traffic = await transitions.GetTraffic(now.AddHours(-1));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            stages.GetByInstanceAndKey(instanceId, "missing-stage"));
        Assert.Null(await compensations.TryGetById(Id.New()));
        Assert.Contains(traffic, point => point.Completed > 0);
    }

    [Fact]
    public void PayloadContextFactoryPreservesNullLegacyTriggerMetadataWhenItIsExplicitlyPresent()
    {
        var metadata = new OrchestrationPropagationMetadata();
        metadata.Items[OrchestrationMetadataConstants.LegacyTriggerMetadataKey] = null!;
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation",
            SagaId = "saga",
            ExecutionKey = "sales.sale.created:correlation",
            Status = OrchestrationInstanceStatus.Running,
            SnapshotPayload = JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}}}"""),
            Metadata =
            {
                [OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
                    JsonSerializer.SerializeToNode(metadata)!
            }
        };
        var factory = new DefaultOrchestrationPayloadContextFactory();

        var context = factory.Create(instance, "stage", "task");

        Assert.True(context.MetadataPayload.AsObject().ContainsKey(OrchestrationMetadataConstants.TriggerMetadataKey));
        Assert.Null(context.MetadataPayload[OrchestrationMetadataConstants.TriggerMetadataKey]);
    }

    [Fact]
    public async Task RuntimeArtifactSerializerAndResolverValidatePayloadAndArtifactStatus()
    {
        var runtimeAssembly = typeof(DefaultOrchestrationBranchNavigator).Assembly;
        var serializerType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts.DefaultRuntimeArtifactSerializer",
            throwOnError: true)!;
        var messagingSerializerType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging.DefaultMessagingCommandSerializer",
            throwOnError: true)!;
        var replyAddressSerializerType = Assembly.Load("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon")
            .GetType(
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.DefaultMessagingReplyAddressSettingsSerializer",
                throwOnError: true)!;
        var resolverType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts.DefaultRuntimeArtifactResolver",
            throwOnError: true)!;
        var serializer = Activator.CreateInstance(serializerType)!;
        var messagingSerializer = Activator.CreateInstance(messagingSerializerType)!;
        var replyAddressSerializer = Activator.CreateInstance(replyAddressSerializerType)!;
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        var resolver = Activator.CreateInstance(resolverType, repository, serializer)!;
        var artifactId = Id.New();
        var runtimeArtifact = new RuntimeOrchestrationArtifact
        {
            Id = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonSerializer.SerializeToNode(Artifact())!,
            Status = RuntimeOrchestrationArtifactStatus.Pending,
            DeployedOnUtc = DateTime.UtcNow
        };
        repository.GetById(artifactId, Arg.Any<CancellationToken>()).Returns(runtimeArtifact);
        var deserializeMethod = serializerType.GetMethod("Deserialize")!;
        var deserializeMessagingMethod = messagingSerializerType.GetMethod("Deserialize")!;
        var deserializeReplyAddressMethod = replyAddressSerializerType.GetMethod("Deserialize")!;
        var resolveMethod = resolverType.GetMethod("ResolveAsync")!;

        Assert.IsType<ArgumentNullException>(InvokeAndUnwrap(() =>
            Activator.CreateInstance(serializerType, [null])));
        Assert.IsType<ArgumentException>(InvokeAndUnwrap(() =>
            deserializeMethod.Invoke(serializer, [" "])));
        Assert.IsType<InvalidOperationException>(InvokeAndUnwrap(() =>
            deserializeMethod.Invoke(serializer, ["null"])));
        Assert.IsType<ArgumentException>(InvokeAndUnwrap(() =>
            deserializeMessagingMethod.Invoke(messagingSerializer, [" "])));
        Assert.IsType<InvalidOperationException>(InvokeAndUnwrap(() =>
            deserializeMessagingMethod.Invoke(messagingSerializer, ["null"])));
        Assert.IsType<ArgumentException>(InvokeAndUnwrap(() =>
            deserializeReplyAddressMethod.Invoke(replyAddressSerializer, [" "])));
        Assert.IsType<InvalidOperationException>(InvokeAndUnwrap(() =>
            deserializeReplyAddressMethod.Invoke(replyAddressSerializer, ["null"])));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            InvokeTaskAndUnwrapAsync(resolveMethod.Invoke(resolver, [" ", CancellationToken.None])!));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeTaskAndUnwrapAsync(resolveMethod.Invoke(resolver, [artifactId.ToString(), CancellationToken.None])!));

        Assert.Equal($"Runtime artifact '{artifactId}' is not active.", exception.Message);
    }

    [Fact]
    public void RuntimeIngressFormattersValidateInputsAndBuildStableKeys()
    {
        var runtimeAssembly = typeof(DefaultOrchestrationBranchNavigator).Assembly;
        var keyBuilderType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Ingress.DefaultRuntimeIngressConfigurationKeyBuilder",
            throwOnError: true)!;
        var backchannelFormatterType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Ingress.DefaultBackchannelMessagingTopicFormatter",
            throwOnError: true)!;
        var keyBuilder = Activator.CreateInstance(keyBuilderType)!;
        var backchannelFormatter = Activator.CreateInstance(backchannelFormatterType, new RuntimeIngressBackchannelOptions())!;
        var buildTriggerKeyMethod = keyBuilderType.GetMethod("BuildTriggerKey")!;
        var buildBackchannelKeyMethod = keyBuilderType.GetMethod("BuildBackchannelKey")!;
        var formatMethod = backchannelFormatterType.GetMethod("Format")!;
        var triggerId = Id.New();
        var trigger = new TriggerBindingArtifact(
            triggerId,
            TriggerType.Event,
            new EventTriggerChannelArtifact(
                new SchemaBindingArtifact(
                    Id.New(),
                    ElementType.Orchestration,
                    triggerId,
                    Id.New(),
                    "sales.sale.created",
                    new SemanticVersion(1, 0, 0),
                    Id.New(),
                    false),
                "events.sales.sale.created",
                new SemanticVersion(1, 0, 0)),
            true,
            "Sale created");
        var artifact = Artifact();

        Assert.Equal($"trigger:{trigger.Id}".ToLowerInvariant(), buildTriggerKeyMethod.Invoke(keyBuilder, [trigger]));
        Assert.Equal("backchannel:messaging", buildBackchannelKeyMethod.Invoke(keyBuilder, [IngressTransport.Messaging]));
        Assert.Equal("orchestrations.sales.sale.created", formatMethod.Invoke(backchannelFormatter, [artifact]));
        Assert.IsType<ArgumentNullException>(InvokeAndUnwrap(() =>
            buildTriggerKeyMethod.Invoke(keyBuilder, [null])));
        Assert.IsType<ArgumentNullException>(InvokeAndUnwrap(() =>
            formatMethod.Invoke(backchannelFormatter, [null])));
    }

    [Fact]
    public async Task BranchNavigatorReturnsNoneWhenNoRuleMatchesOrConditionIsFalse()
    {
        var evaluator = Substitute.For<IOrchestrationConditionEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<OrchestrationConditionEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationConditionEvaluationResult.Success(false));
        var navigator = new DefaultOrchestrationBranchNavigator(evaluator, PayloadContextFactory());
        var sourceId = Id.New();
        var target = Stage(2, "payment");
        var current = Stage(1, "inventory", [Rule(sourceId, target.Id, ElementType.Stage)]);

        var noRule = await navigator.ResolveAsync(Request(current with { BranchRules = [] }, [current], sourceId));
        var falseCondition = await navigator.ResolveAsync(Request(current, [current, target], sourceId));

        Assert.False(noRule.HasNavigation);
        Assert.True(noRule.Succeeded);
        Assert.False(falseCondition.HasNavigation);
        Assert.True(falseCondition.Succeeded);
    }

    [Fact]
    public async Task BranchNavigatorFailsForUnsupportedTargetsMissingStagesAndBackwardNavigation()
    {
        var evaluator = Substitute.For<IOrchestrationConditionEvaluator>();
        evaluator.EvaluateAsync(Arg.Any<OrchestrationConditionEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationConditionEvaluationResult.Success(true));
        var navigator = new DefaultOrchestrationBranchNavigator(evaluator, PayloadContextFactory());
        var sourceId = Id.New();
        var current = Stage(3, "payment");
        var previous = Stage(1, "inventory");
        var unsupported = current with { BranchRules = [Rule(sourceId, Id.New(), ElementType.Task)] };
        var missing = current with { BranchRules = [Rule(sourceId, Id.New(), ElementType.Stage)] };
        var backward = current with { BranchRules = [Rule(sourceId, previous.Id, ElementType.Stage)] };

        var unsupportedResult = await navigator.ResolveAsync(Request(unsupported, [current], sourceId));
        var missingResult = await navigator.ResolveAsync(Request(missing, [current], sourceId));
        var backwardResult = await navigator.ResolveAsync(Request(backward, [previous, current], sourceId));

        Assert.Equal("BranchTargetTypeNotSupported", unsupportedResult.ErrorCode);
        Assert.Equal("BranchTargetNotFound", missingResult.ErrorCode);
        Assert.Equal("BranchTargetOrderNotSupported", backwardResult.ErrorCode);
    }

    [Fact]
    public async Task BranchNavigatorPropagatesConditionFailuresAndNavigatesToForwardStage()
    {
        var evaluator = Substitute.For<IOrchestrationConditionEvaluator>();
        var diagnostics = new Dictionary<string, JsonNode> { ["reason"] = JsonValue.Create("bad-expression")! };
        evaluator.EvaluateAsync(
                Arg.Is<OrchestrationConditionEvaluationRequest>(request => request.Phase == "Branch"),
                Arg.Any<CancellationToken>())
            .Returns(
                OrchestrationConditionEvaluationResult.Failure("DslError", "condition failed", diagnostics),
                OrchestrationConditionEvaluationResult.Success(true));
        var navigator = new DefaultOrchestrationBranchNavigator(evaluator, PayloadContextFactory());
        var sourceId = Id.New();
        var target = Stage(2, "payment");
        var current = Stage(1, "inventory", [Rule(sourceId, target.Id, ElementType.Stage)]);

        var failed = await navigator.ResolveAsync(Request(current, [current, target], sourceId));
        var navigated = await navigator.ResolveAsync(Request(current, [current, target], sourceId));

        Assert.False(failed.Succeeded);
        Assert.Equal("DslError", failed.ErrorCode);
        Assert.Equal("bad-expression", failed.Diagnostics["reason"]!.GetValue<string>());
        Assert.True(navigated.HasNavigation);
        Assert.Equal(target.Id, navigated.TargetStage!.Id);
        await evaluator.Received(2).EvaluateAsync(
            Arg.Is<OrchestrationConditionEvaluationRequest>(request =>
                request.ElementKey == current.BranchRules.Single().Id.ToString() &&
                request.PayloadContext.StageKey == "inventory" &&
                request.PayloadContext.TaskKey == "source-task"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TaskAttemptDispatcherPrivateHelpersCoverDispatchMetadataBranches()
    {
        var now = new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc);
        var instanceId = Id.New();
        var instance = new OrchestrationInstance
        {
            Id = instanceId,
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = string.Empty,
            SagaId = string.Empty,
            ExecutionKey = $"sales.sale.created:{instanceId}",
            Status = OrchestrationInstanceStatus.Running
        };
        var taskExecution = new TaskExecution
        {
            Id = Id.New(),
            TaskKey = "inventories.reserve",
            CorrelationId = string.Empty
        };
        var task = DispatchTask();
        var initialRequest = new TaskAttemptDispatchRequest
        {
            Kind = TaskAttemptDispatchKind.Initial,
            Instance = instance,
            StageExecutionId = Id.New(),
            StageKey = "inventory",
            Task = task
        };
        var retryRequest = new TaskAttemptDispatchRequest
        {
            Kind = TaskAttemptDispatchKind.Retry,
            Instance = instance,
            StageExecutionId = Id.New(),
            StageKey = "inventory",
            Task = task
        };

        var generatedCorrelation = InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveCorrelationId",
            instance,
            taskExecution);
        instance.CorrelationId = "instance-correlation";
        var instanceCorrelation = InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveCorrelationId",
            instance,
            taskExecution);
        taskExecution.CorrelationId = "task-correlation";
        var taskCorrelation = InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveCorrelationId",
            instance,
            taskExecution);

        Assert.False(string.IsNullOrWhiteSpace(generatedCorrelation));
        Assert.Equal("instance-correlation", instanceCorrelation);
        Assert.Equal("task-correlation", taskCorrelation);
        Assert.Equal("TaskDispatchEnqueued", InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveQueuedTransitionType",
            TaskAttemptDispatchKind.Initial,
            false));
        Assert.Equal("TaskRetryScheduled", InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveQueuedTransitionType",
            TaskAttemptDispatchKind.Retry,
            true));
        Assert.Equal("TaskRetryEnqueued", InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveQueuedTransitionType",
            TaskAttemptDispatchKind.Retry,
            false));
        Assert.Equal(TaskExecutionStatus.Pending.ToString(), InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveQueuedFromStatus",
            TaskAttemptDispatchKind.Initial,
            TaskExecutionStatus.Failed));
        Assert.Equal(TaskExecutionStatus.Failed.ToString(), InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "ResolveQueuedFromStatus",
            TaskAttemptDispatchKind.Retry,
            TaskExecutionStatus.Failed));
        Assert.Equal("Task 'inventories.reserve' dispatch enqueued.", InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "BuildQueuedMessage",
            initialRequest,
            1,
            new DateTimeOffset(now, TimeSpan.Zero),
            "Enqueued"));
        Assert.Contains("scheduled", InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "BuildQueuedMessage",
            retryRequest,
            2,
            new DateTimeOffset(now, TimeSpan.Zero),
            "Scheduled"), StringComparison.Ordinal);
        Assert.Contains("enqueued", InvokeTaskAttemptDispatcherPrivateStatic<string>(
            "BuildQueuedMessage",
            retryRequest,
            2,
            new DateTimeOffset(now, TimeSpan.Zero),
            "Enqueued"), StringComparison.Ordinal);
        Assert.Null(InvokeTaskAttemptDispatcherPrivateStatic<JsonNode>("ParsePayload", " "));
        Assert.Equal("sale-1", InvokeTaskAttemptDispatcherPrivateStatic<JsonNode>("ParsePayload", """{"saleId":"sale-1"}""")!["saleId"]!.GetValue<string>());
        Assert.Equal(instanceId.ToString(), InvokeTaskAttemptDispatcherPrivateStatic<string>("GetSagaId", instance));

        instance.SagaId = "saga-1";
        Assert.Equal("saga-1", InvokeTaskAttemptDispatcherPrivateStatic<string>("GetSagaId", instance));
    }

    [Fact]
    public void TaskAttemptDispatcherCreateAndPolicyHelpersCoverSnapshotAndInitialExecutionBranches()
    {
        var now = new DateTime(2026, 8, 20, 14, 30, 0, DateTimeKind.Utc);
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "sales.sale.created:correlation-1",
            Status = OrchestrationInstanceStatus.Running
        };
        var fireAndWait = new TaskAttemptDispatchRequest
        {
            Kind = TaskAttemptDispatchKind.Initial,
            Instance = instance,
            StageExecutionId = Id.New(),
            StageKey = "inventory",
            Task = DispatchTask(TaskDispatchType.FireAndWait)
        };
        var fireAndForget = WithTask(fireAndWait, DispatchTask(TaskDispatchType.FireAndForget));
        var execution = new TaskExecution
        {
            TaskKey = "inventories.reserve"
        };
        var attempt = new TaskExecutionAttempt();
        var dispatch = new TaskDispatch
        {
            DispatchType = "messaging",
            DispatchStatus = "Scheduled"
        };
        var policy = new ResolvedExecutionPolicyArtifact(
            "builtin",
            "local",
            ExecutionPolicyScope.Task,
            ExecutionIsolationRequirement.Required)
        {
            TimeoutSeconds = 30,
            MemoryMb = 128,
            RequireSandboxForExternalExtensions = true
        };

        var waitingExecution = InvokeTaskAttemptDispatcherPrivateStatic<TaskExecution>(
            "CreateInitialTaskExecution",
            fireAndWait,
            now)!;
        var fireAndForgetExecution = InvokeTaskAttemptDispatcherPrivateStatic<TaskExecution>(
            "CreateInitialTaskExecution",
            fireAndForget,
            now)!;
        InvokeTaskAttemptDispatcherPrivateStatic<object>(
            "ApplyExecutionPolicySnapshot",
            execution,
            attempt,
            dispatch,
            policy);

        Assert.True(waitingExecution.AwaitResponse);
        Assert.False(fireAndForgetExecution.AwaitResponse);
        Assert.Equal("correlation-1", waitingExecution.CorrelationId);
        Assert.Equal("builtin", execution.Metadata["ResolvedExecutionProvider"]!.GetValue<string>());
        Assert.Equal("local", execution.Metadata["ResolvedExecutionMode"]!.GetValue<string>());
        Assert.NotNull(attempt.Metadata["ResolvedExecutionPolicy"]);
        Assert.NotNull(dispatch.Metadata["ResolvedExecutionPolicy"]);

        static TaskAttemptDispatchRequest WithTask(TaskAttemptDispatchRequest request, TaskArtifact task)
            => new()
            {
                Kind = request.Kind,
                Instance = request.Instance,
                StageExecutionId = request.StageExecutionId,
                StageKey = request.StageKey,
                Task = task
            };
    }

    private static OrchestrationBranchNavigationRequest Request(
        StageArtifact current,
        IReadOnlyCollection<StageArtifact> stages,
        Id sourceId)
        => new()
        {
            Instance = new OrchestrationInstance
            {
                Id = Id.New(),
                OrchestrationDefinitionKey = "sales.sale.created",
                CorrelationId = "correlation-1",
                SagaId = "saga-1",
                ExecutionKey = "sales.sale.created:correlation-1",
                Status = OrchestrationInstanceStatus.Running,
                StartedOnUtc = DateTime.UtcNow,
                LastUpdatedOnUtc = DateTime.UtcNow,
                SnapshotPayload = JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}}}""")
            },
            Stage = current,
            Stages = stages,
            SourceType = ElementType.Task,
            SourceId = sourceId,
            SourceKey = "source-task"
        };

    private static StageArtifact Stage(int order, string key, IReadOnlyList<BranchRuleArtifact>? rules = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            true,
            null!,
            [],
            [],
            rules ?? []);

    private static BranchRuleArtifact Rule(Id sourceId, Id targetId, ElementType targetType)
        => new(
            Id.New(),
            ElementType.Task,
            sourceId,
            new ExecutionConditionArtifact(
                EngineType.DSL,
                new DslConditionConfigurationArtifact(new Expression("true")))
            {
                IsEnabled = true
            },
            targetType,
            targetId);

    private static IOrchestrationPayloadContextFactory PayloadContextFactory()
    {
        var factory = Substitute.For<IOrchestrationPayloadContextFactory>();
        factory.Create(
                Arg.Any<OrchestrationInstance>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<MetadataDescriptorArtifact>>())
            .Returns(call => new OrchestrationPayloadContext
            {
                ContextPayload = JsonNode.Parse("""{"context":true}"""),
                TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
                StageKey = call.ArgAt<string>(1),
                TaskKey = call.ArgAt<string>(2)
            });
        return factory;
    }

    private static OrchestrationArtifact Artifact()
        => new(
            Id.New(),
            Id.New(),
            "sales.sale.created",
            "Sale created",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("checksum"),
            [],
            [],
            []);

    private static TaskArtifact DispatchTask(TaskDispatchType dispatchType = TaskDispatchType.FireAndWait)
        => new(
            Id.New(),
            "inventories.reserve",
            "Reserve inventory",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null,
            new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact()),
            new MessagingTaskConfigurationArtifact("inventories.reserve", new SemanticVersion(1, 0, 0), null),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            dispatchType,
            true);

    private static Exception InvokeAndUnwrap(Action action)
    {
        try
        {
            action();
            throw new InvalidOperationException("Expected reflected call to throw.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            return exception.InnerException;
        }
    }

    private static async Task InvokeTaskAndUnwrapAsync(object reflectedTask)
    {
        try
        {
            await (Task)reflectedTask;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static T? InvokeTaskAttemptDispatcherPrivateStatic<T>(string methodName, params object?[] args)
    {
        var dispatcherType = typeof(TaskAttemptDispatchRequest).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.TaskAttemptDispatcher",
            throwOnError: true)!;
        var method = Assert.Single(dispatcherType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == args.Length));
        return (T?)method.Invoke(null, args);
    }
}
