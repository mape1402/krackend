using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Runtime.Buffering;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using PigeonVersion = Pigeon.Messaging.Contracts.SemanticVersion;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class PigeonIngressAdapterTests
{
    [Fact]
    public async Task ConnectRegistersPigeonConsumerAndEnqueuesMatchingIngressWork()
    {
        var consuming = new RecordingConsumingConfigurator();
        var selector = Substitute.For<IMessagingIngressConfigurationSelector>();
        var intake = new RecordingIntakeBuffer();
        var messageMetadata = new OrchestrationMessageMetadata
        {
            SagaId = "saga-1",
            OrchestrationInstanceId = "instance-1",
            CorrelationId = "correlation-1"
        };
        var executionMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded",
            ServiceName = "inventories"
        };
        var ingressA = Ingress("artifact-a", IngressKind.Trigger);
        var ingressB = Ingress("artifact-b", IngressKind.Backchannel);
        selector.SelectMatching(Arg.Any<IReadOnlyCollection<IngressConfiguration>>(), "events.sales.sale.created", "1.0.0")
            .Returns([ingressA, ingressB]);
        await using var provider = CreateProvider(
            intake,
            new StaticMessageMetadataAccessor(messageMetadata),
            new StaticExecutionResultMetadataAccessor(executionMetadata),
            new PagedIngressAccessor([
                new IngressConfigurationReadingResult { HasMoreItems = true, Configurations = [ingressA] },
                new IngressConfigurationReadingResult { HasMoreItems = false, Configurations = [ingressB] }
            ]));
        var adapter = CreateAdapter(consuming, selector);

        await adapter.ConnectAsync(new MessagingConfiguration
        {
            ConnectorId = "connector-1",
            Topic = "events.sales.sale.created",
            Version = "1.0.0",
            IngressKind = IngressKind.Trigger,
            IngressTransport = IngressTransport.Messaging
        });

        Assert.NotNull(consuming.Handler);

        var payload = JsonNode.Parse("""{"saleId":"S-1"}""")!;
        await consuming.Handler!(new ConsumeContext
        {
            Services = provider,
            Topic = "events.sales.sale.created",
            Subscription = "Default",
            MessageVersion = PigeonVersion.Parse("1.0.0")
        }, payload);

        Assert.Equal(2, intake.Items.Count);
        Assert.Equal(["artifact-a", "artifact-b"], intake.Items.Select(x => x.ArtifactId));
        Assert.Equal("S-1", intake.Items[0].Payload!["saleId"]!.GetValue<string>());
        Assert.Same(messageMetadata, intake.Items[0].MessageMetadata);
        Assert.Same(executionMetadata, intake.Items[1].ExecutionResultMetadata);
        selector.Received(1).SelectMatching(Arg.Any<IReadOnlyCollection<IngressConfiguration>>(), "events.sales.sale.created", "1.0.0");
    }

    [Fact]
    public async Task ConnectAndDisconnectReferenceCountSharedPigeonEndpoint()
    {
        var consuming = new RecordingConsumingConfigurator();
        var adapter = CreateAdapter(consuming, Substitute.For<IMessagingIngressConfigurationSelector>());
        var first = new MessagingConfiguration
        {
            ConnectorId = "connector-1",
            Topic = "events.sales.sale.created",
            Version = "1.0.0"
        };
        var second = new MessagingConfiguration
        {
            ConnectorId = "connector-2",
            Topic = "events.sales.sale.created",
            Version = "1.0.0"
        };

        await adapter.ConnectAsync(first);
        await adapter.ConnectAsync(second);
        await adapter.DisconnectAsync("connector-1");
        await adapter.DisconnectAsync("connector-2");
        await adapter.DisconnectAsync("missing");

        Assert.Single(consuming.Added);
        Assert.Single(consuming.Removed);
        Assert.Equal("events.sales.sale.created", consuming.Removed.Single().Topic);
    }

    [Fact]
    public async Task ConnectForgetsRegistryWhenPigeonRegistrationThrows()
    {
        var consuming = new RecordingConsumingConfigurator { ThrowOnAdd = true };
        var adapter = CreateAdapter(consuming, Substitute.For<IMessagingIngressConfigurationSelector>());
        var configuration = new MessagingConfiguration
        {
            ConnectorId = "connector-1",
            Topic = "events.sales.sale.created",
            Version = "1.0.0"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ConnectAsync(configuration));
        consuming.ThrowOnAdd = false;

        await adapter.ConnectAsync(configuration);

        Assert.Single(consuming.Added);
    }

    [Fact]
    public async Task ConnectAndDisconnectIgnorePigeonIdempotencyErrors()
    {
        var consuming = new RecordingConsumingConfigurator
        {
            AddException = new InvalidOperationException("Consumer is already registered."),
            RemoveException = new InvalidOperationException("Consumer is not registered.")
        };
        var adapter = CreateAdapter(consuming, Substitute.For<IMessagingIngressConfigurationSelector>());
        var configuration = new MessagingConfiguration
        {
            ConnectorId = "connector-1",
            Topic = "events.sales.sale.created",
            Version = "1.0.0"
        };

        await adapter.ConnectAsync(configuration);
        await adapter.DisconnectAsync(configuration.ConnectorId);

        Assert.Empty(consuming.Added);
        Assert.Single(consuming.Removed);
    }

    private static IMessagingIngressAdapter CreateAdapter(
        IConsumingConfigurator consuming,
        IMessagingIngressConfigurationSelector selector)
    {
        var assembly = typeof(Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        var adapterType = assembly.GetType("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.PigeonIngressAdapter", throwOnError: true)!;
        var registry = Activator.CreateInstance(
            assembly.GetType("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.PigeonIngressConsumerRegistry", throwOnError: true)!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [],
            culture: null)!;
        var logger = Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(adapterType));

        return (IMessagingIngressAdapter)Activator.CreateInstance(
            adapterType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [consuming, registry, selector, logger],
            culture: null)!;
    }

    private static ServiceProvider CreateProvider(
        IIntakeBuffer intake,
        IOrchestrationMessageMetadataAccessor messageMetadata,
        IOrchestrationExecutionResultMetadataAccessor executionResultMetadata,
        IGetAllIngressConfigurationsAccessor ingressAccessor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(intake);
        services.AddSingleton(messageMetadata);
        services.AddSingleton(executionResultMetadata);
        services.AddSingleton(ingressAccessor);
        return services.BuildServiceProvider();
    }

    private static IngressConfiguration Ingress(string artifactId, IngressKind kind)
        => new()
        {
            Id = $"{artifactId}-ingress",
            ArtifactId = artifactId,
            OrchestrationDefinitionKey = "sales.sale.created",
            OrchestrationVersion = "1.0.0",
            DeployedOnUtc = DateTime.UtcNow,
            IngressKind = kind,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = "{}"
        };

    private sealed class RecordingConsumingConfigurator : IConsumingConfigurator
    {
#pragma warning disable CS0067
        public event EventHandler<TopicEventArgs>? TopicCreated;

        public event EventHandler<TopicEventArgs>? TopicRemoved;
#pragma warning restore CS0067

        public List<(string Topic, PigeonVersion Version, string Subscription)> Added { get; } = new();

        public List<(string Topic, PigeonVersion Version)> Removed { get; } = new();

        public ConsumeHandler<JsonNode>? Handler { get; private set; }

        public bool ThrowOnAdd { get; set; }

        public Exception? AddException { get; set; }

        public Exception? RemoveException { get; set; }

        public IConsumingConfigurator AddConsumer<T>(string topic, PigeonVersion version, ConsumeHandler<T> handler)
            where T : class
            => AddConsumer(topic, version, "Default", handler);

        public IConsumingConfigurator AddConsumer<T>(string topic, PigeonVersion version, string subscription, ConsumeHandler<T> handler)
            where T : class
        {
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("boom");
            }

            if (AddException is not null)
            {
                throw AddException;
            }

            Added.Add((topic, version, subscription));
            if (typeof(T) == typeof(JsonNode))
            {
                Handler = (ConsumeHandler<JsonNode>)(object)handler;
            }

            return this;
        }

        public IConsumingConfigurator AddConsumer<T>(string topic, ConsumeHandler<T> handler)
            where T : class
            => AddConsumer(topic, PigeonVersion.Parse("1.0.0"), "Default", handler);

        public IConsumingConfigurator RemoveConsumer(string topic, PigeonVersion version)
        {
            if (RemoveException is not null)
            {
                Removed.Add((topic, version));
                throw RemoveException;
            }

            Removed.Add((topic, version));
            return this;
        }

        public IConsumingConfigurator RemoveConsumer(string topic) => RemoveConsumer(topic, PigeonVersion.Parse("1.0.0"));

        public ConsumerConfiguration GetConfiguration(string topic, PigeonVersion version) => throw new NotSupportedException();

        public ConsumerConfiguration GetConfiguration(string topic, PigeonVersion version, string subscription) => throw new NotSupportedException();

        public ConsumerConfiguration GetConfiguration(string topic) => throw new NotSupportedException();

        public IEnumerable<string> GetAllTopics() => Added.Select(x => x.Topic).Distinct(StringComparer.Ordinal);

        public IEnumerable<ConsumerEndpoint> GetAllEndpoints() => [];
    }

    private sealed class RecordingIntakeBuffer : IIntakeBuffer
    {
        public List<WorkItem> Items { get; } = new();

        public Task EnqueueWorkAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            Items.Add(workItem);
            return Task.CompletedTask;
        }
    }

    private sealed class StaticMessageMetadataAccessor : IOrchestrationMessageMetadataAccessor
    {
        private readonly OrchestrationMessageMetadata _metadata;

        public StaticMessageMetadataAccessor(OrchestrationMessageMetadata metadata) => _metadata = metadata;

        public OrchestrationMessageMetadata Get() => _metadata;
    }

    private sealed class StaticExecutionResultMetadataAccessor : IOrchestrationExecutionResultMetadataAccessor
    {
        private readonly OrchestrationExecutionResultMetadata _metadata;

        public StaticExecutionResultMetadataAccessor(OrchestrationExecutionResultMetadata metadata) => _metadata = metadata;

        public OrchestrationExecutionResultMetadata Get() => _metadata;
    }

    private sealed class PagedIngressAccessor : IGetAllIngressConfigurationsAccessor
    {
        private readonly Queue<IngressConfigurationReadingResult> _pages;

        public PagedIngressAccessor(IEnumerable<IngressConfigurationReadingResult> pages) => _pages = new Queue<IngressConfigurationReadingResult>(pages);

        public Task<IngressConfigurationReadingResult> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_pages.Count == 0
                ? new IngressConfigurationReadingResult { HasMoreItems = false, Configurations = [] }
                : _pages.Dequeue());

        public void Dispose()
        {
        }
    }
}
