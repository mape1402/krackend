using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class StandUpArtifactIngressActionTests
{
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
            NullLogger<StandUpArtifactIngressAction>.Instance);
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
            NullLogger<StandUpArtifactIngressAction>.Instance);
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
}
