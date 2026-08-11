using Krackend.Sagas.Orchestrations.Client;
using Krackend.Sagas.Orchestrations.Client.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Client;

public sealed class OrchestrationClientExecutionCoordinatorTests
{
    [Fact]
    public async Task PublishSuccessUsesMetadataDestinationWhenMetadataExists()
    {
        var metadata = CreateMetadata(responseTopic: "runtime.reply", responseVersion: "3.0.0");
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(metadata, publisher);
        var context = coordinator.Begin(new OrchestrationOutputDescriptor("manual.topic", "2.0.0"));

        await coordinator.PublishSuccess(context, new Request("123"), new Response("Mario"));

        var published = publisher.Request!;
        Assert.Equal(OrchestrationClientExecutionMode.Orchestrated, context.Mode);
        Assert.Equal("runtime.reply", published.Topic);
        Assert.Equal("3.0.0", published.Version.ToString());
        Assert.Same(metadata, published.Metadata);
        Assert.IsType<Response>(published.Payload);
    }

    [Fact]
    public async Task PublishSuccessUsesManualDestinationWhenMetadataDoesNotExist()
    {
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(metadata: null, publisher);
        var context = coordinator.Begin(new OrchestrationOutputDescriptor("manual.topic", "2.0.0"));

        await coordinator.PublishSuccess(context, new Request("123"), new Response("Mario"));

        var published = publisher.Request!;
        Assert.Equal(OrchestrationClientExecutionMode.Standalone, context.Mode);
        Assert.Equal("manual.topic", published.Topic);
        Assert.Equal("2.0.0", published.Version.ToString());
        Assert.Null(published.Metadata);
    }

    [Fact]
    public async Task PublishSuccessSkipsWhenNoMetadataAndNoManualDestinationExist()
    {
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(metadata: null, publisher);
        var context = coordinator.Begin();

        var result = await coordinator.PublishSuccess(context, new Request("123"), new Response("Mario"));

        Assert.True(result.Succeeded);
        Assert.Equal("Skipped", result.Status);
        Assert.Null(publisher.Request);
    }

    [Fact]
    public async Task PublishSuccessWithResponseUsesTransformPayloadBeforePublishing()
    {
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(metadata: null, publisher);
        var context = coordinator.Begin(new OrchestrationOutputDescriptor("manual.topic"));

        await coordinator.PublishSuccess(
            context,
            new Request("123"),
            new Response("Mario"),
            (request, response) => new { request.Id, response.Name });

        var payload = publisher.Request!.Payload;
        Assert.Equal("123", payload.GetType().GetProperty("Id")?.GetValue(payload));
        Assert.Equal("Mario", payload.GetType().GetProperty("Name")?.GetValue(payload));
    }

    [Fact]
    public async Task PublishSuccessWithoutResponseUsesRequestAsDefaultPayload()
    {
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(metadata: null, publisher);
        var context = coordinator.Begin(new OrchestrationOutputDescriptor("manual.topic"));
        var request = new Request("123");

        await coordinator.PublishSuccess(context, request);

        Assert.Same(request, publisher.Request!.Payload);
    }

    [Fact]
    public async Task PublishFailurePublishesDefaultFailurePayload()
    {
        var publisher = new RecordingPublisher();
        var coordinator = CreateCoordinator(metadata: null, publisher);
        var context = coordinator.Begin(new OrchestrationOutputDescriptor("manual.topic"));

        await coordinator.PublishFailure(context, new Request("123"), new InvalidOperationException("boom"));

        var payload = Assert.IsType<OrchestrationFailurePayload>(publisher.Request!.Payload);
        Assert.Equal(typeof(InvalidOperationException).FullName, payload.ErrorType);
        Assert.Equal("boom", payload.Message);
    }

    [Fact]
    public void ServiceCollectionRegistersClientServices()
    {
        var services = new ServiceCollection()
            .AddSingleton<IOrchestrationClientMetadataReader>(new StaticMetadataReader(null))
            .AddSingleton<IOrchestrationOutputPublisher, RecordingPublisher>()
            .AddKrackendSagasOrchestrationsClient();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IOrchestrationClientExecutionCoordinator>());
        Assert.NotNull(provider.GetRequiredService<IOrchestrationClientMetadataAccessor>());
        Assert.NotNull(provider.GetRequiredService<IOrchestrationClientClock>());
    }

    private static OrchestrationClientExecutionCoordinator CreateCoordinator(
        OrchestrationClientMetadata? metadata,
        RecordingPublisher publisher)
        => new(
            new StaticMetadataReader(metadata),
            new RecordingMetadataWriter(),
            publisher,
            new OrchestrationOutputPayloadFactory());

    private static OrchestrationClientMetadata CreateMetadata(string responseTopic, OrchestrationSemanticVersion responseVersion)
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
            CurrentState = new { Status = "Running" },
            ResponseTopic = responseTopic,
            ResponseVersion = responseVersion,
            Environment = "dev"
        };

    private sealed record Request(string Id);

    private sealed record Response(string Name);

    private sealed class StaticMetadataReader : IOrchestrationClientMetadataReader
    {
        private readonly OrchestrationClientMetadata? _metadata;

        public StaticMetadataReader(OrchestrationClientMetadata? metadata)
        {
            _metadata = metadata;
        }

        public OrchestrationClientMetadata? Read() => _metadata;
    }

    private sealed class RecordingMetadataWriter : IOrchestrationClientMetadataWriter
    {
        public OrchestrationClientMetadata? Current { get; private set; }

        public void Set(OrchestrationClientMetadata metadata)
        {
            Current = metadata;
        }

        public void Clear()
        {
            Current = null;
        }
    }

    private sealed class RecordingPublisher : IOrchestrationOutputPublisher
    {
        public OrchestrationPublishRequest? Request { get; private set; }

        public Task<OrchestrationPublishResult> Publish(OrchestrationPublishRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;

            return Task.FromResult(new OrchestrationPublishResult
            {
                Succeeded = true,
                Status = "Dispatched",
                ExternalReference = request.Topic
            });
        }
    }
}
