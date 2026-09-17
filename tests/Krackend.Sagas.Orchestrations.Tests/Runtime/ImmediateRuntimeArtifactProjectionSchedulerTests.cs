using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class ImmediateRuntimeArtifactProjectionSchedulerTests
{
    [Fact]
    public async Task ScheduleProjectionAsync_WhenIngressGenerationChanged_IgnoresStaleRequest()
    {
        var artifact = Artifact(RuntimeOrchestrationArtifactStatus.Pending, ingressGeneration: 2);
        var repository = new ArtifactRepositoryStub(artifact);
        var projector = new RecordingRuntimeIngressConfigurationProjector();
        var unitOfWork = new RecordingRuntimeStorageUnitOfWork();
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        using var provider = CreateProvider(repository, projector, unitOfWork, notifier);
        var scheduler = provider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        await scheduler.ScheduleProjectionAsync(Request(artifact.Id, ingressGeneration: 1));

        Assert.Equal(0, repository.MarkProjectionStartedCount);
        Assert.Equal(0, projector.ProjectCount);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Empty(notifier.Messages);
    }

    [Fact]
    public async Task ScheduleProjectionAsync_WhenArtifactIsPending_ProjectsMarksReadyAndNotifies()
    {
        var artifact = Artifact(RuntimeOrchestrationArtifactStatus.Pending, ingressGeneration: 5);
        var repository = new ArtifactRepositoryStub(artifact);
        var projector = new RecordingRuntimeIngressConfigurationProjector();
        var unitOfWork = new RecordingRuntimeStorageUnitOfWork();
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        using var provider = CreateProvider(repository, projector, unitOfWork, notifier);
        var scheduler = provider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        await scheduler.ScheduleProjectionAsync(Request(artifact.Id, artifact.IngressGeneration));

        Assert.Equal(1, repository.MarkProjectionStartedCount);
        Assert.Equal(1, projector.ProjectCount);
        Assert.Equal(1, repository.MarkReadyCount);
        Assert.Equal(RuntimeOrchestrationArtifactStatus.Ready, artifact.Status);
        Assert.Equal(1, unitOfWork.DeferAutoSaveCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        var message = Assert.Single(notifier.Messages);
        Assert.Equal(artifact.Id.ToString(), message.ArtifactId);
        Assert.Equal("sales.sale.created", message.OrchestrationDefinitionKey);
        Assert.Equal("2.1.0", message.Version);
    }

    [Fact]
    public async Task ScheduleProjectionAsync_WhenArtifactIsAlreadyReady_OnlyNotifiesReadyArtifact()
    {
        var artifact = Artifact(RuntimeOrchestrationArtifactStatus.Ready, ingressGeneration: 6);
        var repository = new ArtifactRepositoryStub(artifact);
        var projector = new RecordingRuntimeIngressConfigurationProjector();
        var unitOfWork = new RecordingRuntimeStorageUnitOfWork();
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        using var provider = CreateProvider(repository, projector, unitOfWork, notifier);
        var scheduler = provider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        await scheduler.ScheduleProjectionAsync(Request(artifact.Id, artifact.IngressGeneration));

        Assert.Equal(0, repository.MarkProjectionStartedCount);
        Assert.Equal(0, projector.ProjectCount);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Single(notifier.Messages);
    }

    [Fact]
    public async Task ScheduleProjectionAsync_WhenProjectionFails_MarksProjectionFailedAndRethrows()
    {
        var artifact = Artifact(RuntimeOrchestrationArtifactStatus.Pending, ingressGeneration: 7);
        var repository = new ArtifactRepositoryStub(artifact);
        var projector = new RecordingRuntimeIngressConfigurationProjector
        {
            Exception = new InvalidOperationException("connector unavailable")
        };
        var unitOfWork = new RecordingRuntimeStorageUnitOfWork();
        var notifier = new RecordingRuntimeArtifactReadyNotifier();
        using var provider = CreateProvider(repository, projector, unitOfWork, notifier);
        var scheduler = provider.GetRequiredService<IRuntimeArtifactProjectionScheduler>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await scheduler.ScheduleProjectionAsync(Request(artifact.Id, artifact.IngressGeneration)));

        Assert.Equal("connector unavailable", exception.Message);
        Assert.Equal(1, repository.MarkProjectionStartedCount);
        Assert.Equal(1, projector.ProjectCount);
        Assert.Equal(1, repository.MarkProjectionFailedCount);
        Assert.Equal("connector unavailable", artifact.ProjectionError);
        Assert.Equal(2, unitOfWork.DeferAutoSaveCount);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        Assert.Empty(notifier.Messages);
    }

    private static ServiceProvider CreateProvider(
        ArtifactRepositoryStub repository,
        RecordingRuntimeIngressConfigurationProjector projector,
        RecordingRuntimeStorageUnitOfWork unitOfWork,
        RecordingRuntimeArtifactReadyNotifier notifier)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsRuntime();
        services.Replace(ServiceDescriptor.Singleton<IRuntimeArtifactRepository>(repository));
        services.Replace(ServiceDescriptor.Singleton<IRuntimeIngressConfigurationProjector>(projector));
        services.Replace(ServiceDescriptor.Singleton<IRuntimeStorageUnitOfWork>(unitOfWork));
        services.Replace(ServiceDescriptor.Singleton<IRuntimeArtifactReadyNotifier>(notifier));
        return services.BuildServiceProvider();
    }

    private static RuntimeArtifactProjectionRequest Request(Id artifactId, long ingressGeneration)
        => new()
        {
            ArtifactId = artifactId.ToString(),
            IngressGeneration = ingressGeneration,
            RequestedBy = "tests",
            RequestedOnUtc = DateTime.UtcNow
        };

    private static RuntimeOrchestrationArtifact Artifact(
        RuntimeOrchestrationArtifactStatus status,
        long ingressGeneration)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = Id.New(),
            Version = new SemanticVersion(2, 1, 0),
            ArtifactChecksum = new Checksum("checksum"),
            ArtifactPayload = JsonNode.Parse("""{"definitionKey":"sales.sale.created"}""")!,
            Status = status,
            IsActive = true,
            IngressGeneration = ingressGeneration,
            DeployedOnUtc = DateTime.UtcNow
        };

    private sealed class ArtifactRepositoryStub(RuntimeOrchestrationArtifact artifact) : IRuntimeArtifactRepository
    {
        public int MarkProjectionStartedCount { get; private set; }

        public int MarkReadyCount { get; private set; }

        public int MarkProjectionFailedCount { get; private set; }

        public Task Upsert(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkProjectionStarted(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
        {
            MarkProjectionStartedCount++;
            artifact.ProjectionStartedOnUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task MarkReady(Id artifactId, long ingressGeneration, CancellationToken cancellationToken = default)
        {
            MarkReadyCount++;
            artifact.Status = RuntimeOrchestrationArtifactStatus.Ready;
            artifact.ProjectionCompletedOnUtc = DateTime.UtcNow;
            artifact.IsActive = true;
            return Task.CompletedTask;
        }

        public Task MarkProjectionFailed(Id artifactId, long ingressGeneration, string error, CancellationToken cancellationToken = default)
        {
            MarkProjectionFailedCount++;
            artifact.Status = RuntimeOrchestrationArtifactStatus.Failed;
            artifact.ProjectionFailedOnUtc = DateTime.UtcNow;
            artifact.ProjectionError = error;
            return Task.CompletedTask;
        }

        public Task DeactivateActiveArtifacts(string orchestrationDefinitionKey, Id exceptArtifactId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuntimeOrchestrationArtifact> GetById(Id artifactId, CancellationToken cancellationToken = default)
            => Task.FromResult(artifact);

        public Task<RuntimeOrchestrationArtifact> GetByVersion(string orchestrationDefinitionKey, SemanticVersion version, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetAll(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<RuntimeOrchestrationArtifact>> GetReady(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuntimeOrchestrationArtifact> GetActive(string orchestrationDefinitionKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingRuntimeIngressConfigurationProjector : IRuntimeIngressConfigurationProjector
    {
        public int ProjectCount { get; private set; }

        public Exception? Exception { get; init; }

        public Task ProjectAsync(RuntimeOrchestrationArtifact artifact, CancellationToken cancellationToken = default)
        {
            ProjectCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeStorageUnitOfWork : IRuntimeStorageUnitOfWork
    {
        public bool AutoSaveChanges => true;

        public int DeferAutoSaveCount { get; private set; }

        public int SaveChangesCount { get; private set; }

        public Task<IRuntimeStorageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IDisposable DeferAutoSave()
        {
            DeferAutoSaveCount++;
            return new NoopDisposable();
        }

        public Task SaveChanges(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeArtifactReadyNotifier : IRuntimeArtifactReadyNotifier
    {
        public List<RuntimeArtifactReadyGossipMessage> Messages { get; } = [];

        public Task NotifyReadyAsync(RuntimeArtifactReadyGossipMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
