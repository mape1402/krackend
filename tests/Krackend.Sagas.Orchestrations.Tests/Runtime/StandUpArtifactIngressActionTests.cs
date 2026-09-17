using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mule;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class StandUpArtifactIngressActionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenLaneBelongsToAnotherReplica_SkipsStandup()
    {
        var repository = new RecordingRuntimeArtifactRepository();
        var registry = Substitute.For<IIngressRegistry>();
        var action = new StandUpArtifactIngressAction(
            repository,
            registry,
            new TestRuntimeReplicaIdentity { LocalStandupLane = "runtime-standup:replica-a" },
            NullLogger<StandUpArtifactIngressAction>.Instance,
            new DefaultMuleTerminalFailureMarker());
        var context = CreateContext(Id.New(), 3, "runtime-standup:replica-b");

        await action.ExecuteAsync(context, CancellationToken.None);

        await registry.DidNotReceiveWithAnyArgs().StandUpOneAsync(default!, default, default);
        Assert.Null(repository.FailedArtifactId);
        Assert.Equal(0, context.Action.Attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLaneMatchesReplica_StandsUpRequestedArtifact()
    {
        var artifactId = Id.New();
        const long IngressGeneration = 3;
        var registry = Substitute.For<IIngressRegistry>();
        var action = new StandUpArtifactIngressAction(
            new RecordingRuntimeArtifactRepository(),
            registry,
            new TestRuntimeReplicaIdentity(),
            NullLogger<StandUpArtifactIngressAction>.Instance,
            new DefaultMuleTerminalFailureMarker());
        var context = CreateContext(artifactId, IngressGeneration, "runtime-standup:replica-a");

        await action.ExecuteAsync(context, CancellationToken.None);

        await registry.Received(1).StandUpOneAsync(
            artifactId.ToString(),
            IngressGeneration,
            Arg.Any<CancellationToken>());
        Assert.Equal(0, context.Action.Attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStandupConfigurationFailureIsPermanent_MarksArtifactFailedAndRequestsTerminalFailure()
    {
        var artifactId = Id.New();
        const long IngressGeneration = 3;
        const string Error = "Missing connector.";

        var repository = new RecordingRuntimeArtifactRepository();
        var action = new StandUpArtifactIngressAction(
            repository,
            new PermanentFailureIngressRegistry(Error),
            new TestRuntimeReplicaIdentity(),
            NullLogger<StandUpArtifactIngressAction>.Instance,
            new DefaultMuleTerminalFailureMarker());
        var request = new RuntimeIngressStandupRequest
        {
            ArtifactId = artifactId.ToString(),
            IngressGeneration = IngressGeneration,
            Reason = "test",
            RequestedOnUtc = DateTime.UtcNow
        };
        var context = new MuleActionContext<RuntimeIngressStandupRequest>(
            new DurableAction
            {
                Key = ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress),
                Lane = "runtime-standup:replica-a",
                CorrelationId = artifactId.ToString(),
                DeduplicationKey = $"{artifactId}:{IngressGeneration}:replica-a",
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            request);

        var exception = await Assert.ThrowsAsync<IngressStandupConfigurationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(Error, exception.Message);
        Assert.Equal(int.MaxValue - 1, context.Action.Attempts);
        Assert.Equal(artifactId, repository.FailedArtifactId);
        Assert.Equal(IngressGeneration, repository.FailedIngressGeneration);
        Assert.Equal(Error, repository.FailedError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStandupFailsTransiently_LeavesFailureRetryable()
    {
        var artifactId = Id.New();
        const long IngressGeneration = 3;
        const string Error = "Broker is unavailable.";

        var repository = new RecordingRuntimeArtifactRepository();
        var action = new StandUpArtifactIngressAction(
            repository,
            new TransientFailureIngressRegistry(Error),
            new TestRuntimeReplicaIdentity(),
            NullLogger<StandUpArtifactIngressAction>.Instance,
            new DefaultMuleTerminalFailureMarker());
        var request = new RuntimeIngressStandupRequest
        {
            ArtifactId = artifactId.ToString(),
            IngressGeneration = IngressGeneration,
            Reason = "test",
            RequestedOnUtc = DateTime.UtcNow
        };
        var context = new MuleActionContext<RuntimeIngressStandupRequest>(
            new DurableAction
            {
                Key = ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress),
                Lane = "runtime-standup:replica-a",
                CorrelationId = artifactId.ToString(),
                DeduplicationKey = $"{artifactId}:{IngressGeneration}:replica-a",
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            request);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(Error, exception.Message);
        Assert.Equal(0, context.Action.Attempts);
        Assert.Null(repository.FailedArtifactId);
        Assert.Null(repository.FailedIngressGeneration);
        Assert.Equal(string.Empty, repository.FailedError);
    }

    private static MuleActionContext<RuntimeIngressStandupRequest> CreateContext(
        Id artifactId,
        long ingressGeneration,
        string lane)
    {
        var request = new RuntimeIngressStandupRequest
        {
            ArtifactId = artifactId.ToString(),
            IngressGeneration = ingressGeneration,
            Reason = "test",
            RequestedOnUtc = DateTime.UtcNow
        };

        return new MuleActionContext<RuntimeIngressStandupRequest>(
            new DurableAction
            {
                Key = ActionKey.From(RuntimeArtifactActionNames.StandUpArtifactIngress),
                Lane = lane,
                CorrelationId = artifactId.ToString(),
                DeduplicationKey = $"{artifactId}:{ingressGeneration}:replica-a",
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            request);
    }
}
