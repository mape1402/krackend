namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
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
        var messageMetadata = new OrchestrationMessageMetadata { SagaId = "saga-1" };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded",
            OperationName = "reserve"
        };

        messageSetter.Set(messageMetadata);
        resultSetter.Set(resultMetadata);

        Assert.Same(messageMetadata, messageAccessor.Get());
        Assert.Same(resultMetadata, resultAccessor.Get());

        messageSetter.Set(null!);
        resultSetter.Clear();

        Assert.NotNull(messageAccessor.Get());
        Assert.Null(messageAccessor.Get().SagaId);
        Assert.Null(resultAccessor.Get());
    }

    [Fact]
    public async Task RuntimeArtifactSerializerAndResolverValidatePayloadAndArtifactStatus()
    {
        var runtimeAssembly = typeof(DefaultOrchestrationBranchNavigator).Assembly;
        var serializerType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts.DefaultRuntimeArtifactSerializer",
            throwOnError: true)!;
        var resolverType = runtimeAssembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts.DefaultRuntimeArtifactResolver",
            throwOnError: true)!;
        var serializer = Activator.CreateInstance(serializerType)!;
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
        var resolveMethod = resolverType.GetMethod("ResolveAsync")!;

        Assert.IsType<ArgumentException>(InvokeAndUnwrap(() =>
            deserializeMethod.Invoke(serializer, [" "])));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            InvokeTaskAndUnwrapAsync(resolveMethod.Invoke(resolver, [" ", CancellationToken.None])));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeTaskAndUnwrapAsync(resolveMethod.Invoke(resolver, [artifactId.ToString(), CancellationToken.None])));

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
        factory.Create(Arg.Any<OrchestrationInstance>(), Arg.Any<string>(), Arg.Any<string>())
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
}
