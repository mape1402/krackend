using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.RetryStrategies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TimeoutBehaviorPolicies;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Branching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Http;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.SchemaRegistry.Resolution;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeSmallComponentBehaviorTests
{
    [Fact]
    public async Task InvokeRemoteCommandActionDispatchesContextAsRemoteCommand()
    {
        RemoteCommand? dispatched = null;
        var dispatcher = Substitute.For<IRemoteCommandDispatcher>();
        dispatcher
            .DispatchAsync(Arg.Do<RemoteCommand>(command => dispatched = command), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(dispatcher);
        await using var provider = services.BuildServiceProvider();
        var payload = """{"saleId":"sale-1"}""";
        var settingsPayload = """{"topic":"commands.inventory.reserve"}""";
        var runtimeAssembly = typeof(IRemoteCommandDispatcher).Assembly;
        var contextType = runtimeAssembly.GetType("Krackend.Sagas.Orchestrations.Runtime.Engine.Actions.InvokeRemoteCommandContext", throwOnError: true)!;
        var actionType = runtimeAssembly.GetType("Krackend.Sagas.Orchestrations.Runtime.Engine.Actions.InvokeRemoteCommandAction", throwOnError: true)!;
        var context = Activator.CreateInstance(contextType, provider)!;
        contextType.GetProperty("Payload")!.SetValue(context, payload);
        contextType.GetProperty("RemoteCommandTransport")!.SetValue(context, RemoteCommandTransport.Messaging);
        contextType.GetProperty("SettingsPayload")!.SetValue(context, settingsPayload);
        var action = Activator.CreateInstance(actionType, context)!;

        await (Task)actionType.GetMethod("ExecuteAsync")!.Invoke(action, [CancellationToken.None])!;

        Assert.NotNull(dispatched);
        Assert.Equal(payload, dispatched!.Payload);
        Assert.Equal(RemoteCommandTransport.Messaging, dispatched.RemoteCommandTransport);
        Assert.Equal(settingsPayload, dispatched.SettingsPayload);
    }

    [Fact]
    public async Task IngressRegistryStandsUpShutsDownAndSchedulersDelegate()
    {
        var reader = Substitute.For<IGetAllIngressConfigurationsAccessor>();
        var byArtifact = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        var connector = Substitute.For<IIngressConector>();
        var first = IngressConfiguration("artifact-1", "ingress-1");
        var second = IngressConfiguration("artifact-2", "ingress-2");
        var one = IngressConfiguration("artifact-3", "ingress-3");
        reader.ReadAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new IngressConfigurationReadingResult
                {
                    HasMoreItems = true,
                    Configurations = [first]
                }),
                Task.FromResult(new IngressConfigurationReadingResult
                {
                    HasMoreItems = false,
                    Configurations = [second]
                }));
        byArtifact.GetConfigurationAsync("artifact-3", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([one]));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddScoped(_ => reader);
        services.AddScoped(_ => byArtifact);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, connector);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IIngressRegistry>();

        await registry.StandUpAllAsync(CancellationToken.None);
        await registry.StandUpOneAsync("artifact-3", 4, CancellationToken.None);
        await registry.StandUpOneAsync("artifact-3", 4, CancellationToken.None);
        await registry.ShutDownOneAsync("artifact-3", CancellationToken.None);
        await registry.ShutDownAllAsync(CancellationToken.None);

        await connector.Received(1).ConnectAsync(first, Arg.Any<CancellationToken>());
        await connector.Received(1).ConnectAsync(second, Arg.Any<CancellationToken>());
        await connector.Received(1).ConnectAsync(one, Arg.Any<CancellationToken>());
        await connector.Received().DisconnectAsync("ingress-3", Arg.Any<CancellationToken>());
        await connector.Received().DisconnectAsync("ingress-1", Arg.Any<CancellationToken>());
        await connector.Received().DisconnectAsync("ingress-2", Arg.Any<CancellationToken>());

        var schedulerRegistry = Substitute.For<IIngressRegistry>();
        var schedulerServices = new ServiceCollection();
        schedulerServices.AddLogging();
        schedulerServices.AddKrackendOrchestrationsRuntime();
        schedulerServices.Replace(ServiceDescriptor.Singleton(schedulerRegistry));
        await using var schedulerProvider = schedulerServices.BuildServiceProvider();
        var scheduler = schedulerProvider.GetRequiredService<IRuntimeIngressStandupScheduler>();
        await scheduler.ScheduleStandupAsync(new RuntimeIngressStandupRequest
        {
            ArtifactId = "artifact-4",
            IngressGeneration = 9,
            Reason = "tests",
            RequestedOnUtc = DateTime.UtcNow
        }, CancellationToken.None);
        await schedulerRegistry.Received(1).StandUpOneAsync("artifact-4", 9, Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => scheduler.ScheduleStandupAsync(null!, CancellationToken.None));

        var backgroundRegistry = Substitute.For<IIngressRegistry>();
        var backgroundService = new IngressRegistryBackgroundService(backgroundRegistry);
        await backgroundService.StartAsync(CancellationToken.None);
        await backgroundService.StopAsync(CancellationToken.None);
        await backgroundRegistry.Received(1).StandUpAllAsync(Arg.Any<CancellationToken>());
        await backgroundRegistry.Received(1).ShutDownAllAsync(Arg.Any<CancellationToken>());

        Assert.Throws<ArgumentNullException>(() => new IngressRegistryBackgroundService(null!));
    }

    [Fact]
    public async Task IngressRegistryFailsWhenArtifactHasNoConfigurationOrConnectorFails()
    {
        var byArtifact = Substitute.For<IGetIngressConfigurationByArtifactAccessor>();
        var connector = Substitute.For<IIngressConector>();
        var failing = IngressConfiguration("artifact-failed", "ingress-failed");
        byArtifact.GetConfigurationAsync("artifact-empty", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([]));
        byArtifact.GetConfigurationAsync("artifact-failed", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([failing]));
        connector.ConnectAsync(failing, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("connector failed")));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.AddScoped(_ => Substitute.For<IGetAllIngressConfigurationsAccessor>());
        services.AddScoped(_ => byArtifact);
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, connector);
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IIngressRegistry>();

        await Assert.ThrowsAsync<IngressStandupConfigurationException>(() =>
            registry.StandUpOneAsync("artifact-empty", 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.StandUpOneAsync("artifact-failed", 1, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.StandUpOneAsync("artifact-failed", 1, CancellationToken.None));

        await connector.Received(2).ConnectAsync(failing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RuntimeDispatchIdempotencyBuildsDeterministicTrimmedKeys()
    {
        var key = RuntimeDispatchIdempotency.Build(new RuntimeDispatchEnvelope
        {
            OrchestrationInstanceId = " instance-1 ",
            DispatchId = " dispatch-1 ",
            TaskExecutionId = " ",
            Attempt = 3,
            CorrelationId = "correlation",
            OrchestrationName = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            StageKey = "stage",
            TaskKey = "task",
            Payload = JsonNode.Parse("{}")!
        });

        Assert.Equal("dispatch|instance-1|dispatch-1|-|3", key);
        Assert.Throws<ArgumentNullException>(() => RuntimeDispatchIdempotency.Build(null!));
    }

    [Fact]
    public void TriggerIntakeBufferResultAndLeaseExposeAcceptedRejectedAndGuardedState()
    {
        var itemId = Id.New();
        var accepted = TriggerIntakeBufferResult.Accept(itemId, "buffered");
        var rejected = TriggerIntakeBufferResult.Reject("duplicate");
        var item = new TriggerIntakeBufferItem
        {
            BufferItemId = itemId,
            TriggerType = TriggerType.Event,
            TriggerKey = "sales.sale.created",
            PayloadJson = "{}"
        };
        var leasedOn = new DateTime(2026, 08, 20, 12, 0, 0, DateTimeKind.Utc);
        var lease = new TriggerIntakeBufferLease(item, "lease-1", leasedOn);

        Assert.True(accepted.Accepted);
        Assert.Equal(itemId, accepted.BufferItemId);
        Assert.Equal("buffered", accepted.Reason);
        Assert.False(rejected.Accepted);
        Assert.Null(rejected.BufferItemId);
        Assert.Equal("duplicate", rejected.Reason);
        Assert.Same(item, lease.Item);
        Assert.Equal("lease-1", lease.LeaseId);
        Assert.Equal(leasedOn, lease.LeasedOnUtc);
        Assert.Throws<ArgumentNullException>(() => new TriggerIntakeBufferLease(null!, "lease", leasedOn));
        Assert.Throws<ArgumentNullException>(() => new TriggerIntakeBufferLease(item, null!, leasedOn));
    }

    [Fact]
    public void SecureConnectionSecretGeneratorCreatesUrlSafeMaterialWithNormalizedPrefix()
    {
        var generator = new SecureConnectionSecretGenerator();

        var credential = generator.GenerateCredential(" Runtime ");
        var fallbackCredential = generator.GenerateCredential(" ");
        var token = generator.GenerateToken();

        Assert.StartsWith("runtime-", credential.ClientId, StringComparison.Ordinal);
        Assert.StartsWith("node-", fallbackCredential.ClientId, StringComparison.Ordinal);
        Assert.StartsWith("key-", credential.KeyId, StringComparison.Ordinal);
        Assert.DoesNotContain("+", credential.ClientSecret, StringComparison.Ordinal);
        Assert.DoesNotContain("/", token, StringComparison.Ordinal);
        Assert.DoesNotContain("=", token, StringComparison.Ordinal);
    }

    [Fact]
    public void DataProtectionRuntimeDesignNodeSecretProtectorRoundTripsSecretsAndRejectsBlankInput()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"krackend-dp-{Guid.NewGuid():N}"));
        var provider = DataProtectionProvider.Create(directory);
        var protector = new DataProtectionRuntimeDesignNodeSecretProtector(provider);

        var protectedValue = protector.Protect("super-secret");

        Assert.NotEqual("super-secret", protectedValue);
        Assert.Equal("super-secret", protector.Unprotect(protectedValue));
        Assert.Throws<ArgumentNullException>(() => new DataProtectionRuntimeDesignNodeSecretProtector(null!));
        Assert.Throws<ArgumentException>(() => protector.Protect(" "));
        Assert.Throws<ArgumentException>(() => protector.Unprotect(""));
    }

    [Fact]
    public void DefaultRuntimeReplicaIdentityNormalizesConfiguredReplicaAndLane()
    {
        var identity = CreateReplicaIdentity(new RuntimeReplicaOptions
        {
            ReplicaId = " Replica A/01 ",
            StandupLanePrefix = " Stand Up Lane "
        });

        Assert.Equal("replica-a-01", identity.ReplicaId);
        Assert.StartsWith("replica-a-01-", identity.ReplicaBootId, StringComparison.Ordinal);
        Assert.Equal("stand-up-lane:replica-a-01", identity.LocalStandupLane);
    }

    [Fact]
    public void DefaultRuntimeReplicaIdentityUsesPodUidAndFallbackLaneWhenConfiguredIdIsMissing()
    {
        var previousPodUid = Environment.GetEnvironmentVariable("POD_UID");
        var previousKubernetesPodUid = Environment.GetEnvironmentVariable("KUBERNETES_POD_UID");
        var previousHostname = Environment.GetEnvironmentVariable("HOSTNAME");
        try
        {
            Environment.SetEnvironmentVariable("POD_UID", " Pod:Runtime ");
            Environment.SetEnvironmentVariable("KUBERNETES_POD_UID", null);
            Environment.SetEnvironmentVariable("HOSTNAME", null);

            var identity = CreateReplicaIdentity(new RuntimeReplicaOptions { StandupLanePrefix = "///" });

            Assert.Equal("pod-runtime", identity.ReplicaId);
            Assert.Equal("runtime-standup:pod-runtime", identity.LocalStandupLane);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POD_UID", previousPodUid);
            Environment.SetEnvironmentVariable("KUBERNETES_POD_UID", previousKubernetesPodUid);
            Environment.SetEnvironmentVariable("HOSTNAME", previousHostname);
        }
    }

    [Fact]
    public void DefaultRuntimeReplicaIdentityUsesKubernetesPodUidHostnameAndMachineFallbacks()
    {
        var previousPodUid = Environment.GetEnvironmentVariable("POD_UID");
        var previousKubernetesPodUid = Environment.GetEnvironmentVariable("KUBERNETES_POD_UID");
        var previousHostname = Environment.GetEnvironmentVariable("HOSTNAME");
        try
        {
            Environment.SetEnvironmentVariable("POD_UID", null);
            Environment.SetEnvironmentVariable("KUBERNETES_POD_UID", " K8S Runtime ");
            Environment.SetEnvironmentVariable("HOSTNAME", null);

            var kubernetes = CreateReplicaIdentity(new RuntimeReplicaOptions());

            Environment.SetEnvironmentVariable("KUBERNETES_POD_UID", null);
            Environment.SetEnvironmentVariable("HOSTNAME", " Runtime Host ");

            var hostname = CreateReplicaIdentity(new RuntimeReplicaOptions());

            Environment.SetEnvironmentVariable("HOSTNAME", null);

            var machine = CreateReplicaIdentity(new RuntimeReplicaOptions());

            Assert.Equal("k8s-runtime", kubernetes.ReplicaId);
            Assert.StartsWith("runtime-host-", hostname.ReplicaId, StringComparison.Ordinal);
            Assert.Contains(Environment.ProcessId.ToString(), machine.ReplicaId, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POD_UID", previousPodUid);
            Environment.SetEnvironmentVariable("KUBERNETES_POD_UID", previousKubernetesPodUid);
            Environment.SetEnvironmentVariable("HOSTNAME", previousHostname);
        }
    }

    [Fact]
    public async Task DefaultRuntimeExecutorsReturnExplicitFallbackResults()
    {
        var validation = new DefaultOrchestrationValidationExecutor();
        var transformation = new DefaultOrchestrationTransformationExecutor();
        var condition = new DefaultOrchestrationConditionEvaluator();
        var payloadContext = new Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads.OrchestrationPayloadContext
        {
            ContextPayload = JsonNode.Parse("{}"),
            TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
            StageKey = "stage",
            TaskKey = "task"
        };

        var validationResult = await validation.ValidateAsync(new OrchestrationValidationRequest
        {
            Phase = "Trigger",
            Payload = JsonNode.Parse("{}"),
        });
        var transformationResult = await transformation.TransformAsync(new OrchestrationTransformationRequest
        {
            Task = TaskArtifact(),
            PayloadContext = payloadContext
        });
        var unsupportedCondition = await condition.EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Phase = "Task",
            ElementKey = "task",
            PayloadContext = payloadContext,
            Condition = new ExecutionConditionArtifact(
                EngineType.DSL,
                new UnsupportedConditionConfigurationArtifact())
            {
                IsEnabled = true
            }
        });
        var missingExpression = await condition.EvaluateAsync(new OrchestrationConditionEvaluationRequest
        {
            Phase = "Task",
            ElementKey = "task",
            PayloadContext = payloadContext,
            Condition = new ExecutionConditionArtifact(
                EngineType.DSL,
                new DslConditionConfigurationArtifact(new Expression(" ")))
            {
                IsEnabled = true
            }
        });

        Assert.Equal("ValidationAdapterNotConfigured", validationResult.ErrorCode);
        Assert.Equal("TransformationAdapterNotConfigured", transformationResult.ErrorCode);
        Assert.Equal("ConditionConfigurationNotSupported", unsupportedCondition.ErrorCode);
        Assert.Equal("ConditionExpressionMissing", missingExpression.ErrorCode);
        await Assert.ThrowsAsync<ArgumentNullException>(() => validation.ValidateAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => transformation.TransformAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => condition.EvaluateAsync(null!));
    }

    [Fact]
    public void RuntimeRequestAndResultContractsExposeConfiguredValues()
    {
        var payloadContext = new OrchestrationPayloadContext
        {
            ContextPayload = JsonNode.Parse("""{"ctx":true}"""),
            TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
            StageKey = "stage-one",
            TaskKey = "inventories.reserve"
        };
        var task = TaskArtifact();
        var stage = new StageArtifact(
            Id.New(),
            "stage-one",
            "Stage one",
            1,
            null!,
            [task],
            [],
            []);
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation",
            SagaId = "saga",
            ExecutionKey = "execution",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };

        var conditionRequest = new OrchestrationConditionEvaluationRequest
        {
            Condition = new ExecutionConditionArtifact(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true"))),
            PayloadContext = payloadContext,
            ElementKey = "task",
            Phase = "Task"
        };
        var branchRequest = new OrchestrationBranchNavigationRequest
        {
            Instance = instance,
            Stages = [stage],
            Stage = stage,
            SourceType = ElementType.Stage,
            SourceId = stage.Id,
            SourceKey = stage.Key
        };
        var validationRequest = new OrchestrationValidationRequest
        {
            Task = task,
            SchemaBinding = null!,
            Payload = JsonNode.Parse("{}"),
            ValidationDsl = "validate {}",
            Phase = "Request"
        };
        var transformationRequest = new OrchestrationTransformationRequest
        {
            Task = task,
            PayloadContext = payloadContext
        };
        var preparationRequest = new TaskDispatchRequestPayloadPreparationRequest
        {
            Instance = instance,
            StageKey = stage.Key,
            Task = task,
            MessagingConfiguration = new MessagingTaskConfigurationArtifact(
                "commands.inventories.reserve",
                new SemanticVersion(1, 0, 0),
                null!),
            Payload = """{"saleId":"sale-1"}"""
        };

        Assert.Equal("Task", conditionRequest.Phase);
        Assert.Equal(stage.Id, branchRequest.SourceId);
        Assert.False(OrchestrationConditionEvaluationResult.Failure("ConditionFailed", "no").Succeeded);
        Assert.True(OrchestrationBranchNavigationResult.None().Succeeded);
        Assert.Equal(stage.Id, OrchestrationBranchNavigationResult.Navigate(Id.New(), stage).TargetStage.Id);
        Assert.Equal("BranchFailed", OrchestrationBranchNavigationResult.Failure("BranchFailed", "no").ErrorCode);
        Assert.Equal("Request", validationRequest.Phase);
        Assert.True(OrchestrationValidationResult.Success().Succeeded);
        Assert.Equal("ValidationFailed", OrchestrationValidationResult.Failure("ValidationFailed", "no").ErrorCode);
        Assert.Equal(task.Key, transformationRequest.Task.Key);
        Assert.True(OrchestrationTransformationResult.Success(JsonNode.Parse("{}")!).Succeeded);
        Assert.Equal("TransformFailed", OrchestrationTransformationResult.Failure("TransformFailed", "no").ErrorCode);
        Assert.Equal("""{"saleId":"sale-1"}""", preparationRequest.Payload);
        Assert.Equal("stage-one", payloadContext.StageKey);
        Assert.True(((JsonObject)new TaskDispatchRequestPayloadPreparationResult { Payload = JsonNode.Parse("""{"ready":true}""")! }.Payload)["ready"]!.GetValue<bool>());
    }

    [Fact]
    public async Task DefaultIngressConnectorsForwardMessagingConfigurationAndIgnoreHttp()
    {
        var adapter = Substitute.For<IMessagingIngressAdapter>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Singleton(adapter));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var connector = scope.ServiceProvider.GetRequiredKeyedService<IIngressConector>(IngressTransport.Messaging);
        var configuration = new IngressConfiguration
        {
            Id = "connector-1",
            ArtifactId = "artifact-1",
            IngressKind = IngressKind.Backchannel,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = """{"topic":"orchestrations.sales.sale.created","version":"1.0.0"}"""
        };

        await connector.ConnectAsync(configuration);
        await connector.DisconnectAsync("connector-1");

        await adapter.Received(1).ConnectAsync(
            Arg.Is<MessagingConfiguration>(value =>
                value.ConnectorId == "connector-1" &&
                value.ArtifactId == "artifact-1" &&
                value.IngressKind == IngressKind.Backchannel &&
                value.IngressTransport == IngressTransport.Messaging &&
                value.Topic == "orchestrations.sales.sale.created"),
            Arg.Any<CancellationToken>());
        await adapter.Received(1).DisconnectAsync("connector-1", Arg.Any<CancellationToken>());

        var http = scope.ServiceProvider.GetRequiredKeyedService<IIngressConector>(IngressTransport.Http);
        await http.ConnectAsync(configuration);
        await http.DisconnectAsync("connector-1");
    }

    [Fact]
    public async Task SnapshotSchemaContractResolverReturnsResolvedAndNotFoundResults()
    {
        var reference = new SchemaContractReference
        {
            ProviderKey = "snapshot",
            ContractKey = "sales.sale.created",
            ContractVersion = "1.0.0",
            Kind = SchemaContractKind.Event
        };
        var snapshot = new Krackend.Sagas.Orchestrations.SchemaRegistry.SchemaContractSnapshot
        {
            Reference = reference,
            ContentHash = "hash"
        };
        var store = new InMemorySchemaContractSnapshotStore();
        store.Set(snapshot);
        var resolver = new SnapshotSchemaContractResolver(store);

        var resolved = await resolver.ResolveAsync(new SchemaContractResolutionRequest { Reference = reference });
        var missing = await resolver.ResolveAsync(new SchemaContractResolutionRequest
        {
            Reference = reference with { ContractVersion = "2.0.0" }
        });

        Assert.Equal("snapshot", resolver.ProviderKey);
        Assert.Equal(SchemaContractResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("hash", resolved.Snapshot!.ContentHash);
        Assert.Equal(SchemaContractResolutionStatus.NotFound, missing.Status);
        Assert.Contains("sales.sale.created", missing.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => new SnapshotSchemaContractResolver(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveAsync(null!));
    }

    [Fact]
    public void DefinitionDefaultsBuildExpectedTypedDefaults()
    {
        var condition = InvokeDefinitionDefault<ExecutionCondition>("CreateExecutionCondition");
        var retry = InvokeDefinitionDefault<RetryPolicy>("CreateRetryPolicy");
        var timeout = InvokeDefinitionDefault<TimeoutPolicy>("CreateTimeoutPolicy");
        var transformation = InvokeDefinitionDefault<TransformationDefinition>("CreateTransformationDefinition");
        var http = InvokeDefinitionDefault<ITaskConfiguration>("CreateTaskConfiguration", TaskKind.Http);
        var messaging = InvokeDefinitionDefault<ITaskConfiguration>("CreateTaskConfiguration", TaskKind.Messaging);
        var plugin = InvokeDefinitionDefault<ITaskConfiguration>("CreateTaskConfiguration", TaskKind.Plugin);
        var human = InvokeDefinitionDefault<ITaskConfiguration>("CreateTaskConfiguration", TaskKind.HumanApproval);
        var messagingCompensation = InvokeDefinitionDefault<CompensationDefinition>("CreateCompensationDefinition", TaskKind.Messaging);
        var httpCompensation = InvokeDefinitionDefault<CompensationDefinition>("CreateCompensationDefinition", TaskKind.Http);

        Assert.Equal("true", Assert.IsType<DslConditionConfiguration>(condition.Configuration).Expression.ToString());
        Assert.IsType<FixedRetryStrategy>(retry.Strategy);
        Assert.Equal(TimeSpan.FromSeconds(30), timeout.Timeout.Value);
        Assert.IsType<FailTimeoutBehaviorPolicy>(timeout.TimeoutBehaviorPolicy);
        Assert.IsType<DslTransformationConfiguration>(transformation.Configuration);
        Assert.IsType<HttpTaskConfiguration>(http);
        Assert.IsType<MessagingTaskConfiguration>(messaging);
        Assert.IsType<PluginTaskConfiguration>(plugin);
        Assert.IsType<HumanApprovalTaskConfiguration>(human);
        Assert.Equal(TaskDispatchType.FireAndForget, messagingCompensation.DispatchType);
        Assert.Equal(TaskDispatchType.FireAndWait, httpCompensation.DispatchType);
    }

    [Fact]
    public void DefaultTimeoutProcessorParsesDateMetadataDefensively()
    {
        var type = typeof(IOrchestrationTimeoutProcessor).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts.DefaultOrchestrationTimeoutProcessor",
            throwOnError: true)!;
        var method = type.GetMethod("TryGetDateTime", BindingFlags.Static | BindingFlags.NonPublic)!;
        var validDate = DateTime.UtcNow;

        var dateValue = InvokeTryGetDateTime(method, new Dictionary<string, JsonNode>
        {
            ["value"] = JsonValue.Create(validDate)!
        });
        var stringValue = InvokeTryGetDateTime(method, new Dictionary<string, JsonNode>
        {
            ["value"] = JsonValue.Create(validDate.ToString("O"))!
        });
        var invalidString = InvokeTryGetDateTime(method, new Dictionary<string, JsonNode>
        {
            ["value"] = JsonValue.Create("not-a-date")!
        });
        var invalidObject = InvokeTryGetDateTime(method, new Dictionary<string, JsonNode>
        {
            ["value"] = JsonNode.Parse("""{"not":"a date"}""")!
        });
        var missing = InvokeTryGetDateTime(method, new Dictionary<string, JsonNode>());
        var nullMetadata = InvokeTryGetDateTime(method, null!);

        Assert.True(dateValue.Succeeded);
        Assert.Equal(validDate, dateValue.Value);
        Assert.True(stringValue.Succeeded);
        Assert.False(invalidString.Succeeded);
        Assert.False(invalidObject.Succeeded);
        Assert.False(missing.Succeeded);
        Assert.False(nullMetadata.Succeeded);
    }

    [Fact]
    public async Task TimeoutBackgroundServiceProcessesAndLogsFailures()
    {
        var processor = new RecordingTimeoutProcessor();
        var services = new ServiceCollection();
        services.AddScoped<IOrchestrationTimeoutProcessor>(_ => processor);
        await using var provider = services.BuildServiceProvider();
        var service = CreateTimeoutBackgroundService(provider.GetRequiredService<IServiceScopeFactory>());
        var method = service.GetType().GetMethod("ProcessTimeoutsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)method.Invoke(service, [CancellationToken.None])!;
        processor.Throw = true;
        await (Task)method.Invoke(service, [CancellationToken.None])!;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        processor.ThrowOperationCanceled = true;
        await (Task)method.Invoke(service, [cancellation.Token])!;

        Assert.Equal(3, processor.Calls);
    }

    [Fact]
    public async Task TimeoutBackgroundServiceExecuteLoopProcessesWhenEnabled()
    {
        var processor = new SignalingTimeoutProcessor();
        var services = new ServiceCollection();
        services.AddScoped<IOrchestrationTimeoutProcessor>(_ => processor);
        await using var provider = services.BuildServiceProvider();
        var service = (IHostedService)CreateTimeoutBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new OrchestrationTimeoutOptions
            {
                Enabled = true,
                ScanIntervalSeconds = 0
            });

        await service.StartAsync(CancellationToken.None);
        await processor.Processed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        Assert.True(processor.Calls >= 1);
    }

    [Fact]
    public async Task DefaultTimeoutProcessorSkipsTasksThatNoLongerNeedTimeoutHandling()
    {
        var now = DateTime.UtcNow;
        var taskId = Id.New();
        var task = WaitingTask(taskId, Id.New(), now.AddMinutes(-5));
        task.Status = TaskExecutionStatus.Running;
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        taskRepository.GetWaitingResponseOlderThan(now, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([task]));
        taskRepository.GetById(taskId, Arg.Any<CancellationToken>()).Returns(task);
        var processor = CreateTimeoutProcessor(taskRepository: taskRepository);

        var processed = await processor.ProcessDueTimeoutsAsync(now);

        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task DefaultTimeoutProcessorSkipsTasksWhenInstanceIsTerminal()
    {
        var now = DateTime.UtcNow;
        var instanceId = Id.New();
        var task = WaitingTask(Id.New(), instanceId, now.AddMinutes(-5));
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        taskRepository.GetWaitingResponseOlderThan(now, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<TaskExecution>>([task]));
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        instanceRepository.GetById(instanceId, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationInstance
            {
                Id = instanceId,
                RuntimeOrchestrationArtifactId = Id.New(),
                OrchestrationDefinitionKey = "sales.sale.created",
                CorrelationId = "correlation",
                SagaId = "saga",
                ExecutionKey = "sales.sale.created",
                Status = OrchestrationInstanceStatus.Completed,
                StartedOnUtc = now,
                LastUpdatedOnUtc = now
            });
        var processor = CreateTimeoutProcessor(instanceRepository, taskRepository: taskRepository);

        var processed = await processor.ProcessDueTimeoutsAsync(now);

        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task DefaultTimeoutProcessorSkipsTasksWithoutTimeoutPolicy()
    {
        var now = DateTime.UtcNow;
        var artifactId = Id.New();
        var stageId = Id.New();
        var instanceId = Id.New();
        var task = WaitingTask(Id.New(), instanceId, now.AddMinutes(-5));
        task.StageExecutionId = stageId;
        await using var provider = CreateRuntimeProvider();
        using var scope = provider.CreateScope();
        await SeedTimeoutRuntimeAsync(
            scope.ServiceProvider,
            artifactId,
            RunningInstance(instanceId, artifactId, now),
            Stage(stageId, instanceId, now),
            task,
            TaskArtifactWithoutTimeout());
        var processor = scope.ServiceProvider.GetRequiredService<IOrchestrationTimeoutProcessor>();

        var processed = await processor.ProcessDueTimeoutsAsync(now);

        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task DefaultTimeoutProcessorSkipsTasksWhenNoWaitingAttemptExists()
    {
        var now = DateTime.UtcNow;
        var artifactId = Id.New();
        var stageId = Id.New();
        var instanceId = Id.New();
        var task = WaitingTask(Id.New(), instanceId, now.AddMinutes(-5));
        task.StageExecutionId = stageId;
        await using var provider = CreateRuntimeProvider();
        using var scope = provider.CreateScope();
        await SeedTimeoutRuntimeAsync(
            scope.ServiceProvider,
            artifactId,
            RunningInstance(instanceId, artifactId, now),
            Stage(stageId, instanceId, now),
            task,
            TaskArtifact());
        await scope.ServiceProvider.GetRequiredService<ITaskExecutionAttemptRepository>().Create(new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running
        });
        var processor = scope.ServiceProvider.GetRequiredService<IOrchestrationTimeoutProcessor>();

        var processed = await processor.ProcessDueTimeoutsAsync(now);

        Assert.Equal(0, processed);
    }

    private static IRuntimeReplicaIdentity CreateReplicaIdentity(RuntimeReplicaOptions options)
    {
        var type = typeof(IRuntimeReplicaIdentity).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Replication.DefaultRuntimeReplicaIdentity",
            throwOnError: true)!;
        return (IRuntimeReplicaIdentity)Activator.CreateInstance(type, Options.Create(options))!;
    }

    private static TResult InvokeDefinitionDefault<TResult>(string methodName, params object[] parameters)
    {
        var type = typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ServiceCollectionExtensions)
            .Assembly
            .GetType(
                "Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Infrastructure.DefinitionDefaults",
                throwOnError: true)!;
        var method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName &&
                method.GetParameters().Length == parameters.Length);

        return (TResult)method.Invoke(null, parameters)!;
    }

    private static (bool Succeeded, DateTime Value) InvokeTryGetDateTime(
        MethodInfo method,
        IReadOnlyDictionary<string, JsonNode> metadata)
    {
        var parameters = new object?[] { metadata, "value", default(DateTime) };
        var succeeded = (bool)method.Invoke(null, parameters)!;
        return (succeeded, (DateTime)parameters[2]!);
    }

    private static object CreateTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        OrchestrationTimeoutOptions? options = null)
    {
        var serviceType = typeof(IOrchestrationTimeoutProcessor).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts.OrchestrationTimeoutBackgroundService",
            throwOnError: true)!;
        var logger = Activator.CreateInstance(typeof(ReflectionLogger<>).MakeGenericType(serviceType));

        return Activator.CreateInstance(
            serviceType,
            scopeFactory,
            new StaticOptionsMonitor<OrchestrationTimeoutOptions>(options ?? new OrchestrationTimeoutOptions()),
            logger)!;
    }

    private static IOrchestrationTimeoutProcessor CreateTimeoutProcessor(
        IOrchestrationInstanceRepository? instanceRepository = null,
        IStageExecutionRepository? stageRepository = null,
        ITaskExecutionRepository? taskRepository = null,
        ITaskExecutionAttemptRepository? attemptRepository = null,
        ITaskDispatchRepository? dispatchRepository = null,
        IExecutionTransitionRepository? transitionRepository = null)
    {
        var type = typeof(IOrchestrationTimeoutProcessor).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts.DefaultOrchestrationTimeoutProcessor",
            throwOnError: true)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        object ArgumentFor(Type parameterType)
        {
            if (parameterType == typeof(IOrchestrationInstanceRepository))
            {
                return instanceRepository ?? Substitute.For<IOrchestrationInstanceRepository>();
            }

            if (parameterType == typeof(IStageExecutionRepository))
            {
                return stageRepository ?? Substitute.For<IStageExecutionRepository>();
            }

            if (parameterType == typeof(ITaskExecutionRepository))
            {
                return taskRepository ?? Substitute.For<ITaskExecutionRepository>();
            }

            if (parameterType == typeof(ITaskExecutionAttemptRepository))
            {
                return attemptRepository ?? Substitute.For<ITaskExecutionAttemptRepository>();
            }

            if (parameterType == typeof(ITaskDispatchRepository))
            {
                return dispatchRepository ?? Substitute.For<ITaskDispatchRepository>();
            }

            if (parameterType == typeof(IExecutionTransitionRepository))
            {
                return transitionRepository ?? Substitute.For<IExecutionTransitionRepository>();
            }

            return CreateRuntimeProvider().GetRequiredService(parameterType);
        }

        return (IOrchestrationTimeoutProcessor)constructor.Invoke(
            constructor.GetParameters().Select(parameter => ArgumentFor(parameter.ParameterType)).ToArray());
    }

    private static TaskExecution WaitingTask(Id taskId, Id instanceId, DateTime waitingSinceUtc)
        => new()
        {
            Id = taskId,
            OrchestrationInstanceId = instanceId,
            StageExecutionId = Id.New(),
            TaskKey = "inventories.reserve",
            Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = waitingSinceUtc
        };

    private static OrchestrationInstance RunningInstance(Id instanceId, Id artifactId, DateTime now)
        => new()
        {
            Id = instanceId,
            RuntimeOrchestrationArtifactId = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "correlation",
            SagaId = "saga",
            ExecutionKey = "sales.sale.created",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = now,
            LastUpdatedOnUtc = now
        };

    private static StageExecution Stage(Id stageId, Id instanceId, DateTime now)
        => new()
        {
            Id = stageId,
            OrchestrationInstanceId = instanceId,
            StageKey = "sale-fulfillment",
            Status = StageExecutionStatus.Running,
            StartedOnUtc = now
        };

    private static ServiceProvider CreateRuntimeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        return services.BuildServiceProvider();
    }

    private static async Task SeedTimeoutRuntimeAsync(
        IServiceProvider services,
        Id artifactId,
        OrchestrationInstance instance,
        StageExecution stage,
        TaskExecution task,
        TaskArtifact taskArtifact)
    {
        var artifact = BuildOrchestrationArtifact(taskArtifact);
        await services.GetRequiredService<IRuntimeArtifactRepository>().Upsert(new RuntimeOrchestrationArtifact
        {
            Id = artifactId,
            OrchestrationDefinitionKey = artifact.Key,
            ArtifactType = "orchestration",
            SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
            Version = artifact.Version,
            ArtifactChecksum = artifact.Checksum,
            ArtifactPayload = JsonSerializer.SerializeToNode(artifact)!,
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        });
        await services.GetRequiredService<IOrchestrationInstanceRepository>().Create(instance);
        await services.GetRequiredService<IStageExecutionRepository>().Create(stage);
        await services.GetRequiredService<ITaskExecutionRepository>().Create(task);
    }

    private static OrchestrationArtifact BuildOrchestrationArtifact(TaskArtifact task)
        => new(
            Id.New(),
            Id.New(),
            "sales.sale.created",
            "Sale Created",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("hash"),
            [],
            [],
            [
                new StageArtifact(
                    Id.New(),
                    "sale-fulfillment",
                    "Sale fulfillment",
                    1,
                    null!,
                    [task],
                    [],
                    [])
            ]);

    private static TaskArtifact TaskArtifactWithoutTimeout()
    {
        var task = TaskArtifact();
        return task with { TimeoutPolicy = null! };
    }

    private static TaskArtifact TaskArtifact()
        => new(
            Id.New(),
            "inventories.reserve",
            "Reserve inventory",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null!,
            null!,
            new MessagingTaskConfigurationArtifact(
                "commands.inventories.reserve",
                new SemanticVersion(1, 0, 0),
                null!),
            new RetryPolicyArtifact(
                0,
                RetryStrategyType.Fixed,
                new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
                [],
                true),
            new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Fail,
                new FailTimeoutBehaviorPolicyArtifact("Timeout")),
            OnErrorPolicy.Stop,
            null!,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static IngressConfiguration IngressConfiguration(string artifactId, string id)
        => new()
        {
            Id = id,
            ArtifactId = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            DeployedOnUtc = DateTime.UtcNow,
            IngressTransport = IngressTransport.Messaging,
            IngressKind = IngressKind.Trigger,
            SettingsPayload = "{}"
        };

    private sealed class UnsupportedConditionConfigurationArtifact : IConditionConfigurationArtifact
    {
        public EngineType Engine => EngineType.DSL;
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class RecordingTimeoutProcessor : IOrchestrationTimeoutProcessor
    {
        public int Calls { get; private set; }

        public bool Throw { get; set; }

        public bool ThrowOperationCanceled { get; set; }

        public Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            Calls++;

            if (ThrowOperationCanceled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("processor failed");
            }

            return Task.FromResult(1);
        }
    }

    private sealed class SignalingTimeoutProcessor : IOrchestrationTimeoutProcessor
    {
        public TaskCompletionSource Processed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public Task<int> ProcessDueTimeoutsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            Calls++;
            Processed.TrySetResult();
            return Task.FromResult(1);
        }
    }

    private sealed class ReflectionLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
