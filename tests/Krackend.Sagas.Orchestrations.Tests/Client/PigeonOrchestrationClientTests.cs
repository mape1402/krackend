using Krackend.Sagas.Orchestrations.Client;
using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Krackend.Sagas.Orchestrations.Client.Pigeon;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Producing;
using PigeonSemanticVersion = Pigeon.Messaging.Contracts.SemanticVersion;

namespace Krackend.Sagas.Orchestrations.Tests.Client;

public sealed class PigeonOrchestrationClientTests
{
    [Fact]
    public void PigeonAdapterExposesExpectedPublicSurface()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Client.Pigeon",
            typeof(ClientPigeonMarker).Namespace);
    }

    [Fact]
    public void MetadataReaderMapsPigeonMetadataToClientMetadata()
    {
        var runtimeMetadata = CreateRuntimeMetadata();
        var reader = new PigeonOrchestrationClientMetadataReader(new StaticOrchestratorMetadataAccessor(runtimeMetadata));

        var metadata = reader.Read();

        Assert.Equal(runtimeMetadata.SagaId, metadata.SagaId);
        Assert.Equal(runtimeMetadata.ResponseTopic, metadata.ResponseTopic);
        Assert.Equal(runtimeMetadata.ResponseVersion, metadata.ResponseVersion.ToString());
        Assert.Same(runtimeMetadata.CurrentState, metadata.CurrentState);
    }

    [Fact]
    public void SemanticVersionConvertsToPigeonSemanticVersionAtAdapterBoundary()
    {
        OrchestrationSemanticVersion version = "2.4.6";

        var pigeonVersion = version.ToPigeonSemanticVersion();

        Assert.Equal("2.4.6", pigeonVersion.ToString());
    }

    [Fact]
    public async Task OutputPublisherPublishesThroughPigeonWithMetadataAndSemanticVersion()
    {
        var producer = new RecordingProducer();
        var writer = new RecordingOrchestratorMetadataWriter();
        var publisher = new PigeonOrchestrationOutputPublisher(producer, writer);
        var metadata = CreateClientMetadata();

        var result = await publisher.Publish(new OrchestrationPublishRequest
        {
            Topic = "orders.reply",
            Version = "2.1.3",
            Payload = new { Id = "123" },
            Metadata = metadata
        });

        Assert.True(result.Succeeded);
        Assert.Equal("orders.reply", producer.Topic);
        Assert.Equal("2.1.3", producer.Version.ToString());
        Assert.Equal(metadata.SagaId, writer.LastSet?.SagaId);
        Assert.Null(writer.Current);
    }

    [Fact]
    public void ServiceCollectionRegistersPigeonClientServices()
    {
        var services = new ServiceCollection()
            .AddSingleton<IProducer, RecordingProducer>()
            .AddKrackendSagasOrchestrationsClientPigeon();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<PigeonOrchestrationClientMetadataReader>(
            provider.GetRequiredService<IOrchestrationClientMetadataReader>());
        Assert.IsType<PigeonOrchestrationOutputPublisher>(
            provider.GetRequiredService<IOrchestrationOutputPublisher>());
        Assert.NotNull(provider.GetRequiredService<IOrchestrationClientExecutionCoordinator>());
    }

    private static OrchestratorMessageMetadata CreateRuntimeMetadata()
        => new()
        {
            SagaId = "saga-1",
            OrchestrationId = "orchestration-1",
            OrchestrationKey = "orders",
            OrchestrationVersion = "1.0.0",
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            DispatchId = "dispatch-1",
            CorrelationId = "correlation-1",
            CurrentState = new OrchestrationRuntimeState
            {
                Status = "Running",
                CurrentStageKey = "stage",
                CurrentTaskKey = "task",
                Attempt = 1,
                StartedOnUtc = DateTime.UtcNow,
                UpdatedOnUtc = DateTime.UtcNow
            },
            ResponseTopic = "runtime.reply",
            ResponseVersion = "3.0.0",
            Environment = "dev"
        };

    private static OrchestrationClientMetadata CreateClientMetadata()
    {
        var runtimeMetadata = CreateRuntimeMetadata();

        return new OrchestrationClientMetadata
        {
            SagaId = runtimeMetadata.SagaId,
            OrchestrationId = runtimeMetadata.OrchestrationId,
            OrchestrationKey = runtimeMetadata.OrchestrationKey,
            OrchestrationVersion = runtimeMetadata.OrchestrationVersion,
            OrchestrationInstanceId = runtimeMetadata.OrchestrationInstanceId,
            TaskExecutionId = runtimeMetadata.TaskExecutionId,
            DispatchId = runtimeMetadata.DispatchId,
            CorrelationId = runtimeMetadata.CorrelationId,
            CurrentState = runtimeMetadata.CurrentState,
            ResponseTopic = runtimeMetadata.ResponseTopic,
            ResponseVersion = runtimeMetadata.ResponseVersion,
            Environment = runtimeMetadata.Environment
        };
    }

    private sealed class StaticOrchestratorMetadataAccessor : IOrchestratorMetadataAccessor
    {
        public StaticOrchestratorMetadataAccessor(OrchestratorMessageMetadata current)
        {
            Current = current;
        }

        public OrchestratorMessageMetadata Current { get; }
    }

    private sealed class RecordingOrchestratorMetadataWriter : IOrchestratorMetadataWriter
    {
        public OrchestratorMessageMetadata? Current { get; private set; }

        public OrchestratorMessageMetadata? LastSet { get; private set; }

        public void Set(OrchestratorMessageMetadata metadata)
        {
            Current = metadata;
            LastSet = metadata;
        }

        public void Clear()
        {
            Current = null;
        }
    }

    private sealed class RecordingProducer : IProducer
    {
        public object? Message { get; private set; }

        public string? Topic { get; private set; }

        public PigeonSemanticVersion Version { get; private set; }

        public ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
            where T : class
            => PublishAsync(message, topic, PigeonSemanticVersion.Default, cancellationToken);

        public ValueTask PublishAsync<T>(T message, string topic, PigeonSemanticVersion version, CancellationToken cancellationToken = default)
            where T : class
        {
            Message = message;
            Topic = topic;
            Version = version;

            return ValueTask.CompletedTask;
        }
    }
}
