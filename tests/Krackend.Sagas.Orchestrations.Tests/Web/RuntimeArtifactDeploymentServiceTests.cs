using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Web;

namespace Krackend.Sagas.Orchestrations.Tests.Web;

public sealed class RuntimeArtifactDeploymentServiceTests
{
    [Fact]
    public async Task Deploy_AcceptsArtifactWithoutRequestEnvironmentAndSynchronizesHotIngress()
    {
        var repository = new CapturingRuntimeArtifactRepository();
        var synchronizer = new CapturingRuntimeIngressSynchronizer();
        var service = new RuntimeArtifactDeploymentService(
            new RuntimeEnvironmentDescriptor("local"),
            repository,
            synchronizer);

        var result = await service.Deploy(CreateRequest(environmentKey: null));

        Assert.True(result.Accepted);
        Assert.Equal("", result.EnvironmentKey);
        Assert.NotNull(repository.Upserted);
        Assert.Equal("local", repository.Upserted.EnvironmentKey);
        Assert.Equal("sales.sale.created", repository.Upserted.OrchestrationDefinitionKey);
        Assert.Same(repository.Upserted, synchronizer.SynchronizedArtifact);
    }

    private static RuntimeArtifactDeploymentRequest CreateRequest(string environmentKey)
    {
        var definitionId = Id.New();
        var versionId = Id.New();
        var artifact = new OrchestrationArtifact(
            definitionId,
            versionId,
            "sales.sale.created",
            "Sale Created",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("checksum"),
            Array.Empty<TriggerBindingArtifact>(),
            Array.Empty<VariableDefinitionArtifact>(),
            Array.Empty<StageArtifact>());

        return new RuntimeArtifactDeploymentRequest
        {
            ArtifactId = Id.New().ToString(),
            ArtifactType = "orchestration.deploy",
            EnvironmentKey = environmentKey,
            OrchestrationDefinitionId = definitionId.ToString(),
            OrchestrationVersionId = versionId.ToString(),
            OrchestrationDefinitionKey = artifact.Key,
            Version = "1.0.0",
            Checksum = "checksum",
            PayloadJson = JsonSerializer.Serialize(artifact),
            CorrelationId = "corr-1",
            PromotedBy = "test"
        };
    }

    private sealed class CapturingRuntimeArtifactRepository : IRuntimeArtifactRepository
    {
        public RuntimeOrchestrationArtifact Upserted { get; private set; }

        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        {
            Upserted = artifact;
            return Task.CompletedTask;
        }

        public Task DeactivateActiveArtifacts(string environmentKey, string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RuntimeOrchestrationArtifact> GetByVersion(string environmentKey, string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(string environmentKey, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RuntimeOrchestrationArtifact> GetActive(string environmentKey, string orchestrationDefinitionKey, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class CapturingRuntimeIngressSynchronizer : IRuntimeIngressSynchronizer
    {
        public RuntimeOrchestrationArtifact SynchronizedArtifact { get; private set; }

        public Task SynchronizeActiveArtifacts(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SynchronizeArtifact(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        {
            SynchronizedArtifact = artifact;
            return Task.CompletedTask;
        }
    }
}
