namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mule;
using NSubstitute;
using System.Text.Json.Nodes;

public sealed class ProjectIngressConfigurationsActionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenProjectionConfigurationFailureIsPermanent_MarksArtifactFailedAndRequestsTerminalFailure()
    {
        var artifactId = Id.New();
        const long IngressGeneration = 7;
        const string Error = "Artifact does not contain messaging triggers.";
        var artifact = CreateArtifact(artifactId, IngressGeneration);
        var repository = CreateRepository(artifact);
        var projector = Substitute.For<IRuntimeIngressConfigurationProjector>();
        projector
            .ProjectAsync(artifact, Arg.Any<CancellationToken>())
            .Returns(_ => throw new IngressProjectionConfigurationException(Error));
        var action = CreateAction(repository, projector);
        var context = CreateContext(artifactId, IngressGeneration);

        var exception = await Assert.ThrowsAsync<IngressProjectionConfigurationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(Error, exception.Message);
        Assert.Equal(int.MaxValue - 1, context.Action.Attempts);
        await repository.Received(1).MarkProjectionFailed(
            artifactId,
            IngressGeneration,
            Error,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectionFailsTransiently_LeavesFailureRetryable()
    {
        var artifactId = Id.New();
        const long IngressGeneration = 7;
        const string Error = "Database is temporarily unavailable.";
        var artifact = CreateArtifact(artifactId, IngressGeneration);
        var repository = CreateRepository(artifact);
        var projector = Substitute.For<IRuntimeIngressConfigurationProjector>();
        projector
            .ProjectAsync(artifact, Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException(Error));
        var action = CreateAction(repository, projector);
        var context = CreateContext(artifactId, IngressGeneration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(Error, exception.Message);
        Assert.Equal(0, context.Action.Attempts);
        await repository.Received(1).MarkProjectionFailed(
            artifactId,
            IngressGeneration,
            Error,
            Arg.Any<CancellationToken>());
    }

    private static ProjectIngressConfigurationsAction CreateAction(
        IRuntimeArtifactRepository repository,
        IRuntimeIngressConfigurationProjector projector)
        => new(
            repository,
            projector,
            new NoopRuntimeStorageUnitOfWork(),
            Substitute.For<IRuntimeArtifactReadyNotifier>(),
            NullLogger<ProjectIngressConfigurationsAction>.Instance,
            new DefaultMuleTerminalFailureMarker());

    private static IRuntimeArtifactRepository CreateRepository(RuntimeOrchestrationArtifact artifact)
    {
        var repository = Substitute.For<IRuntimeArtifactRepository>();
        repository
            .GetById(artifact.Id, Arg.Any<CancellationToken>())
            .Returns(artifact);
        repository
            .MarkProjectionStarted(artifact.Id, artifact.IngressGeneration, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        repository
            .MarkProjectionFailed(artifact.Id, artifact.IngressGeneration, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static RuntimeOrchestrationArtifact CreateArtifact(Id artifactId, long ingressGeneration)
        => new()
        {
            Id = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse("{}")!,
            Status = RuntimeOrchestrationArtifactStatus.Pending,
            IngressGeneration = ingressGeneration,
            DeployedOnUtc = DateTime.UtcNow
        };

    private static MuleActionContext<RuntimeArtifactProjectionRequest> CreateContext(
        Id artifactId,
        long ingressGeneration)
    {
        var request = new RuntimeArtifactProjectionRequest
        {
            ArtifactId = artifactId.ToString(),
            IngressGeneration = ingressGeneration,
            RequestedBy = "test",
            RequestedOnUtc = DateTime.UtcNow
        };

        return new MuleActionContext<RuntimeArtifactProjectionRequest>(
            new DurableAction
            {
                Key = ActionKey.From(RuntimeArtifactActionNames.ProjectIngressConfigurations),
                Lane = RuntimeArtifactProjectionSchedulerDefaults.Lane,
                CorrelationId = artifactId.ToString(),
                DeduplicationKey = $"{artifactId}:{ingressGeneration}",
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            request);
    }
}
