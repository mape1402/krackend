using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class TriggerPromoterVersionResolutionTests
{
    [Fact]
    public async Task Promote_UsesRequestedArtifactVersionAndDefaultsToActiveWhenVersionIsMissing()
    {
        var store = new VersionedTriggerStore();
        var v1 = CreateArtifact("1.0.0", isActive: false);
        var v2 = CreateArtifact("2.0.0", isActive: true);
        store.Artifacts[v1.Id] = v1;
        store.Artifacts[v2.Id] = v2;
        var promoter = CreatePromoter(store);

        var explicitV1 = await promoter.Promote(CreateItem("order-v1", "1.0.0"), CancellationToken.None);
        var active = await promoter.Promote(CreateItem("order-active", string.Empty), CancellationToken.None);
        var replay = await promoter.Promote(CreateItem("order-v1", "1.0.0"), CancellationToken.None);

        Assert.Equal(v1.Id, explicitV1.Artifact.Id);
        Assert.Equal(v1.Id, explicitV1.Instance.RuntimeOrchestrationArtifactId);
        Assert.Equal(v2.Id, active.Artifact.Id);
        Assert.Equal(v2.Id, active.Instance.RuntimeOrchestrationArtifactId);
        Assert.Equal(explicitV1.Instance.Id, replay.Instance.Id);
        Assert.Equal(v1.Id, replay.Artifact.Id);
        Assert.Equal("1.0.0", explicitV1.Instance.Metadata["artifactVersion"]!.GetValue<string>());
    }

    [Fact]
    public async Task Promote_AllowsDistinctIdempotencyKeysToShareCorrelationId()
    {
        var store = new VersionedTriggerStore();
        var artifact = CreateArtifact("1.0.0", isActive: true);
        store.Artifacts[artifact.Id] = artifact;
        var promoter = CreatePromoter(store);

        var first = await promoter.Promote(CreateItem("shared-correlation", "1.0.0", "first-idempotency"), CancellationToken.None);
        var second = await promoter.Promote(CreateItem("shared-correlation", "1.0.0", "second-idempotency"), CancellationToken.None);
        var replay = await promoter.Promote(CreateItem("shared-correlation", "1.0.0", "first-idempotency"), CancellationToken.None);

        Assert.Equal("shared-correlation", first.Instance.CorrelationId);
        Assert.Equal("shared-correlation", second.Instance.CorrelationId);
        Assert.NotEqual(first.Instance.Id, second.Instance.Id);
        Assert.NotEqual(first.Instance.ExecutionKey, second.Instance.ExecutionKey);
        Assert.Equal(first.Instance.Id, replay.Instance.Id);
        Assert.EndsWith(first.Intake.Id.ToString(), first.Instance.ExecutionKey);
        Assert.EndsWith(second.Intake.Id.ToString(), second.Instance.ExecutionKey);
    }

    private static TriggerPromoter CreatePromoter(VersionedTriggerStore store)
        => new(
            new ArtifactResolver(new RuntimeArtifactRepositoryStub(store)),
            new TriggerIntakeRepositoryStub(store),
            new InstanceRepositoryStub(store),
            new TransitionRepositoryStub(store));

    private static TriggerIntakeBufferItem CreateItem(string correlationId, string artifactVersion, string idempotencyKey = "")
        => new()
        {
            TriggerType = TriggerType.Event,
            TriggerKey = "order.fulfillment",
            ArtifactVersion = artifactVersion,
            EnvironmentKey = "local",
            CorrelationId = correlationId,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? correlationId : idempotencyKey,
            SourceMessageId = $"msg-{correlationId}",
            PayloadJson = """{"orderId":"A1"}"""
        };

    private static RuntimeOrchestrationArtifact CreateArtifact(string version, bool isActive)
        => new()
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            ArtifactType = "orchestration.deploy",
            SourceOrchestrationVersionId = Id.New(),
            Version = ParseVersion(version),
            ArtifactChecksum = new Checksum($"checksum-{version}"),
            ArtifactPayload = JsonNode.Parse($$"""
            {
              "Key": "order.fulfillment",
              "Version": "{{version}}",
              "Stages": []
            }
            """),
            IsActive = isActive,
            DeployedOnUtc = DateTime.UtcNow,
            ActivatedOnUtc = isActive ? DateTime.UtcNow : null
        };

    private static SemanticVersion ParseVersion(string value)
    {
        var parts = value.Split('.');
        return new SemanticVersion(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    private sealed class VersionedTriggerStore
    {
        public Dictionary<Id, RuntimeOrchestrationArtifact> Artifacts { get; } = new();
        public Dictionary<string, TriggerIntake> IntakesByIdempotencyKey { get; } = new(StringComparer.Ordinal);
        public Dictionary<Id, OrchestrationInstance> Instances { get; } = new();
        public List<ExecutionTransition> Transitions { get; } = new();
    }

    private sealed class RuntimeArtifactRepositoryStub(VersionedTriggerStore store) : IRuntimeArtifactRepository
    {
        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default) => Task.FromResult(store.Artifacts[artifactId]);
        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<RuntimeOrchestrationArtifact>>(store.Artifacts.Values.ToArray());
        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(store.Artifacts.Values.Single(x => x.EnvironmentKey == environmentKey && x.OrchestrationDefinitionKey == orchestrationDefinitionKey && x.IsActive));
        public Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
            => Task.FromResult<RuntimeOrchestrationArtifact>(store.Artifacts.Values.SingleOrDefault(x => x.EnvironmentKey == environmentKey && x.OrchestrationDefinitionKey == orchestrationDefinitionKey && x.Version.ToString() == version.ToString())!);
    }

    private sealed class TriggerIntakeRepositoryStub(VersionedTriggerStore store) : ITriggerIntakeRepository
    {
        public Task Create(TriggerIntake intake, CancellationToken cancellationToken = default)
        {
            store.IntakesByIdempotencyKey[$"{intake.EnvironmentKey}::{intake.IdempotencyKey}"] = intake;
            return Task.CompletedTask;
        }

        public Task Update(TriggerIntake intake, CancellationToken cancellationToken = default)
        {
            store.IntakesByIdempotencyKey[$"{intake.EnvironmentKey}::{intake.IdempotencyKey}"] = intake;
            return Task.CompletedTask;
        }

        public Task<TriggerIntake> GetById(Id intakeId, CancellationToken cancellationToken = default)
            => Task.FromResult(store.IntakesByIdempotencyKey.Values.Single(x => x.Id == intakeId));

        public Task<TriggerIntake> GetByIdempotencyKey(string environmentKey, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            store.IntakesByIdempotencyKey.TryGetValue($"{environmentKey}::{idempotencyKey}", out var intake);
            return Task.FromResult<TriggerIntake>(intake!);
        }
    }

    private sealed class InstanceRepositoryStub(VersionedTriggerStore store) : IOrchestrationInstanceRepository
    {
        public Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            store.Instances[instance.Id] = instance;
            return Task.CompletedTask;
        }

        public Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default) => Task.FromResult(store.Instances[instanceId]);
        public Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TransitionRepositoryStub(VersionedTriggerStore store) : IExecutionTransitionRepository
    {
        public Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            store.Transitions.Add(transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
