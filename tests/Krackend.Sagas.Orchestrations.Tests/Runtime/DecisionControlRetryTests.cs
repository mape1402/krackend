namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class DecisionControlRetryTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DecideAsyncReturnsRetryWhenExecutionErrorCodeIsRetryable()
    {
        var state = await CreateRetryStateAsync();
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(new DecisionRequest
        {
            ArtifactId = state.ArtifactId.ToString(),
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = state.InstanceId.ToString()
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                ErrorCode = "TemporaryInventoryFailure"
            },
            Payload = JsonNode.Parse("""{"ignored":"business payload should not decide retry"}""")
        }, CancellationToken.None);

        Assert.Contains(decisions, decision => decision.Kind == "retry-task");
    }

    [Fact]
    public async Task DecideAsyncDoesNotRetryWhenExecutionErrorCodeIsNotRetryable()
    {
        var state = await CreateRetryStateAsync();
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(new DecisionRequest
        {
            ArtifactId = state.ArtifactId.ToString(),
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = state.InstanceId.ToString()
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                ErrorCode = "PermanentInventoryFailure"
            },
            Payload = JsonNode.Parse("""{"errorCode":"TemporaryInventoryFailure"}""")
        }, CancellationToken.None);

        Assert.DoesNotContain(decisions, decision => decision.Kind == "retry-task");
    }

    private static async Task<RetryState> CreateRetryStateAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var artifactRepository = scopedServices.GetRequiredService<IRuntimeArtifactRepository>();
        var instanceRepository = scopedServices.GetRequiredService<IOrchestrationInstanceRepository>();
        var stageRepository = scopedServices.GetRequiredService<IStageExecutionRepository>();
        var taskRepository = scopedServices.GetRequiredService<ITaskExecutionRepository>();

        var artifactId = Id.New();
        var instanceId = Id.New();
        var stageExecutionId = Id.New();
        var taskExecutionId = Id.New();
        var artifact = CreateArtifact();

        await artifactRepository.Upsert(new RuntimeOrchestrationArtifact
        {
            Id = artifactId,
            OrchestrationDefinitionKey = artifact.Key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
            Version = artifact.Version,
            ArtifactChecksum = artifact.Checksum,
            ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions),
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            IngressGeneration = 1,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        });
        await instanceRepository.Create(new OrchestrationInstance
        {
            Id = instanceId,
            OrchestrationDefinitionKey = artifact.Key,
            RuntimeOrchestrationArtifactId = artifactId,
            CorrelationId = "sale-1",
            ExecutionKey = $"{artifact.Key}:{instanceId}",
            Status = OrchestrationInstanceStatus.Running,
            CurrentStageKey = "inventory",
            CurrentTaskKey = "inventories.reserve",
            SnapshotPayload = JsonNode.Parse(
                """
                {
                  "trigger": {
                    "payload": {
                      "saleId": "sale-1"
                    }
                  },
                  "stages": {},
                  "variables": {}
                }
                """)
        });
        await stageRepository.Create(new StageExecution
        {
            Id = stageExecutionId,
            OrchestrationInstanceId = instanceId,
            StageKey = "inventory",
            Order = 1,
            Status = StageExecutionStatus.Running
        });
        await taskRepository.Create(new TaskExecution
        {
            Id = taskExecutionId,
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageExecutionId,
            TaskKey = "inventories.reserve",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.Failed,
            OnErrorPolicy = OnErrorPolicy.Stop,
            AwaitResponse = true,
            LastAttemptNumber = 1,
            CorrelationId = "sale-1"
        });

        return new RetryState(provider, artifactId, instanceId);
    }

    private static OrchestrationArtifact CreateArtifact()
    {
        var retryPolicy = new RetryPolicyArtifact(
            2,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            ["TemporaryInventoryFailure"],
            true);
        var task = new TaskArtifact(
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
            retryPolicy,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWait,
            true);
        var stage = new StageArtifact(
            Id.New(),
            "inventory",
            "Inventory",
            1,
            null,
            [task],
            [],
            []);

        return new OrchestrationArtifact(
            Id.New(),
            Id.New(),
            "sales.sale.created",
            "Sale Created",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("decision-control-retry-test"),
            [],
            [],
            [stage]);
    }

    private sealed record RetryState(IServiceProvider Services, Id ArtifactId, Id InstanceId);
}
