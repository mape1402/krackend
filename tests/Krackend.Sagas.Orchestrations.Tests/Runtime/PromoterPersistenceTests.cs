namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Persistence;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using NSubstitute;

public sealed class PromoterPersistenceTests
{
    [Fact]
    public async Task PromoteToInstancePersistsTheInstanceAndPromotionTransitionInOneStateTransaction()
    {
        var fixture = new PromotionFixture();

        var result = await fixture.Promoter.PromoteToInstanceAsync(new PromotionRequest
        {
            ArtifactId = fixture.ResolvedArtifact.RuntimeArtifact.Id.ToString(),
            Payload = JsonNode.Parse("{\"orderId\":\"order-1\"}"),
            MessageMetadata = new OrchestrationMessageMetadata { CorrelationId = "corr-1" },
            StartIdempotencyKey = "ingress-1"
        });

        Assert.True(result.Success);
        Assert.Equal(1, fixture.StateTransaction.ExecutionCount);
        var instance = Assert.Single(fixture.Instances);
        Assert.Equal(OrchestrationInstanceStatus.Created, instance.Status);
        Assert.Equal("corr-1", instance.CorrelationId);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal(instance.Id, transition.OrchestrationInstanceId);
        Assert.Equal("InstancePromoted", transition.TransitionType);
    }

    private sealed class PromotionFixture
    {
        public PromotionFixture()
        {
            ArtifactResolver.ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(ResolvedArtifact));
            InstanceRepository.TryGetByStartIdempotencyKey(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<OrchestrationInstance>(null!));
            InstanceRepository
                .Create(Arg.Do<OrchestrationInstance>(instance => Instances.Add(instance)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            PayloadState.CreateInitialPayload(Arg.Any<JsonNode>()).Returns(JsonNode.Parse("{\"trigger\":{}}"));
            TriggerPayloadValidator.ValidateAsync(Arg.Any<ResolvedOrchestrationArtifact>(), Arg.Any<JsonNode>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(OrchestrationValidationResult.Success()));
            Promoter = new Promoter(
                ArtifactResolver,
                InstanceRepository,
                TransitionRepository,
                ArtifactAccessor,
                PayloadState,
                TriggerPayloadValidator,
                stateTransaction: StateTransaction);
        }

        public IRuntimeArtifactResolver ArtifactResolver { get; } = Substitute.For<IRuntimeArtifactResolver>();

        public IOrchestrationInstanceRepository InstanceRepository { get; } = Substitute.For<IOrchestrationInstanceRepository>();

        public IExecutionTransitionRepository TransitionRepository { get; } = Substitute.For<IExecutionTransitionRepository>();

        public IResolvedOrchestrationArtifactAccessor ArtifactAccessor { get; } = Substitute.For<IResolvedOrchestrationArtifactAccessor>();

        public IOrchestrationPayloadState PayloadState { get; } = Substitute.For<IOrchestrationPayloadState>();

        public ITriggerPayloadValidator TriggerPayloadValidator { get; } = Substitute.For<ITriggerPayloadValidator>();

        public RecordingStateTransaction StateTransaction { get; } = new();

        public List<OrchestrationInstance> Instances { get; } = [];

        public List<ExecutionTransition> Transitions { get; } = [];

        public ResolvedOrchestrationArtifact ResolvedArtifact { get; } = CreateResolvedArtifact();

        public Promoter Promoter { get; }
    }

    private static ResolvedOrchestrationArtifact CreateResolvedArtifact()
    {
        var artifact = new OrchestrationArtifact(
            Id.New(),
            Id.New(),
            "orders_created",
            "Orders created",
            "orders",
            new SemanticVersion(1, 0, 0),
            new Checksum("promotion-test"),
            [],
            [],
            []);
        return new ResolvedOrchestrationArtifact
        {
            RuntimeArtifact = new RuntimeOrchestrationArtifact
            {
                Id = Id.New(),
                OrchestrationDefinitionKey = artifact.Key,
                ArtifactType = "orchestration.deploy",
                Version = artifact.Version,
                ArtifactChecksum = artifact.Checksum,
                ArtifactPayload = JsonNode.Parse("{}")!
            },
            Artifact = artifact
        };
    }

    private sealed class RecordingStateTransaction : IRuntimeStateTransaction
    {
        public int ExecutionCount { get; private set; }

        public async Task ExecuteAsync(Func<CancellationToken, Task> mutation, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            await mutation(cancellationToken);
        }
    }
}
