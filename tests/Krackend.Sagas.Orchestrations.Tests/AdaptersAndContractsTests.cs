using System.Reflection;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using Spider.Pipelines.Core;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class AdaptersAndContractsTests
{
    [Fact]
    public void ContractsCarryVersionLifecycleEventData()
    {
        var deployed = new OrchestrationVersionDeployedEvent(
            "version-1",
            "definition-1",
            "Sale Created",
            "v1",
            "1.0.0",
            "{}",
            "checksum",
            "tester",
            "correlation",
            DateTime.UtcNow);
        var deprecated = new OrchestrationVersionDeprecatedEvent(
            deployed.OrchestrationVersionId,
            deployed.OrchestrationDefinitionId,
            deployed.OrchestrationDisplayName,
            deployed.VersionLabel,
            deployed.VersionNumber,
            deployed.ArtifactPayloadJson,
            deployed.Checksum,
            deployed.Actor,
            deployed.CorrelationId,
            deployed.OccurredAtUtc);
        var archived = new OrchestrationVersionArchivedEvent(
            deployed.OrchestrationVersionId,
            deployed.OrchestrationDefinitionId,
            deployed.OrchestrationDisplayName,
            deployed.VersionLabel,
            deployed.VersionNumber,
            deployed.ArtifactPayloadJson,
            deployed.Checksum,
            deployed.Actor,
            deployed.CorrelationId,
            deployed.OccurredAtUtc);

        Assert.Equal("version-1", deployed.OrchestrationVersionId);
        Assert.Equal(deployed.VersionNumber, deprecated.VersionNumber);
        Assert.Equal(deployed.CorrelationId, archived.CorrelationId);
    }

    [Fact]
    public async Task ClientPigeonPublisherValidatesTransportDeserializesAddressAndPublishes()
    {
        var producer = new RecordingProducer();
        var serializer = CreateInternal(
            typeof(Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions).Assembly,
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.DefaultMessagingReplyAddressSettingsSerializer");
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Failed"
        };
        var resultMetadataAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        resultMetadataAccessor.Get().Returns(resultMetadata);
        var publisher = (IOrchestrationClientPublisher)CreateInternal(
            typeof(Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions).Assembly,
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.PigeonOrchestrationClientPublisher",
            producer,
            serializer,
            resultMetadataAccessor);
        var address = new OrchestrationReplyAddress
        {
            Transport = OrchestrationTransportNames.Messaging,
            SettingsPayload = JsonSerializer.Serialize(new MessagingReplyAddressSettings
            {
                Topic = "orchestrations.sales.sale.created",
                Version = "1.2.3"
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

        await publisher.PublishAsync(new { ok = true }, address);

        Assert.Equal("orchestrations.sales.sale.created", producer.Topic);
        Assert.Equal("1.2.3", producer.Version?.ToString());

        await publisher.PublishAsync(null!, address);

        Assert.IsType<System.Text.Json.Nodes.JsonObject>(producer.Payload);
        Assert.True(resultMetadata.Metadata[OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey]!.GetValue<bool>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync(new { ok = false }, null!));
        await Assert.ThrowsAsync<NotSupportedException>(() => publisher.PublishAsync(
            new { ok = false },
            new OrchestrationReplyAddress { Transport = "http", SettingsPayload = "{}" }));

        var deserialize = serializer.GetType().GetMethod("Deserialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        Assert.Throws<TargetInvocationException>(() => deserialize.Invoke(serializer, [""]));
    }

    [Fact]
    public async Task RuntimePigeonDispatchAdapterParsesPayloadAndPublishes()
    {
        var producer = new RecordingProducer();
        var adapter = (IMessagingDispatchAdapter)CreateInternal(
            typeof(Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions).Assembly,
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.PigeonDispatchAdapter",
            producer);

        await adapter.PublishAsync(new MessagingCommand
        {
            Topic = "inventories.reserve",
            Version = "2.1.0",
            Payload = """{"sku":"ABC"}"""
        });
        await adapter.PublishAsync(new MessagingCommand
        {
            Topic = "inventories.release",
            Version = "2.1.0",
            Payload = ""
        });

        Assert.Equal("inventories.release", producer.Topic);
        Assert.Equal("2.1.0", producer.Version?.ToString());
        Assert.Null(producer.Payload);
    }

    [Fact]
    public void ClientPigeonExtensionRegistersPublisherAndValidatesArguments()
    {
        KrackendOrchestrationsClientBuilder nullBuilder = null!;
        var services = new ServiceCollection();
        var builder = services.AddKrackendOrchestrationsClient();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pigeon:Domain"] = "Krackend.Tests",
                ["Pigeon:MessageBrokers:RabbitMq:Url"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(nullBuilder));
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(nullBuilder, configuration, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, configuration, null!));

        var returned = Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder);
        var configured = Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(
            builder,
            configuration,
            _ => { });

        Assert.Same(builder, returned);
        Assert.Same(builder, configured);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IOrchestrationClientPublisher) &&
            descriptor.ImplementationType?.Name == "PigeonOrchestrationClientPublisher");
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.Name == "IConsumeInterceptor" &&
            descriptor.ImplementationType?.Name == "KrackendClientConsumeInterceptor");
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.Name == "IPublishInterceptor" &&
            descriptor.ImplementationType?.Name == "KrackendClientPublishInterceptor");
    }

    [Fact]
    public void RuntimePigeonExtensionRegistersMessagingAdaptersAndValidatesArguments()
    {
        KrackendOrchestrationsRuntimeBuilder nullBuilder = null!;
        var services = new ServiceCollection();
        var builder = services.AddKrackendOrchestrationsRuntime();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pigeon:Domain"] = "Krackend.Tests",
                ["Pigeon:MessageBrokers:RabbitMq:Url"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(nullBuilder, configuration));
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, configuration, null!));

        var returned = Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, configuration);

        Assert.Same(builder, returned);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMessagingDispatchAdapter));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging.IMessagingIngressAdapter));
    }

    [Fact]
    public void RuntimePigeonIngressRegistryTracksSharedEndpointReferenceCounts()
    {
        var assembly = typeof(Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        var registry = CreateInternal(
            assembly,
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.PigeonIngressConsumerRegistry");
        var first = CreateInternal(
            assembly,
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.PigeonIngressConsumerRegistration",
            "connector-1",
            "topic|1.0.0|default",
            "topic",
            "1.0.0",
            "Default");
        var second = CreateInternal(
            assembly,
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.PigeonIngressConsumerRegistration",
            "connector-2",
            "topic|1.0.0|default",
            "topic",
            "1.0.0",
            "Default");

        var tryAttach = registry.GetType().GetMethod("TryAttach")!;
        var tryDetach = registry.GetType().GetMethod("TryDetach")!;
        var forget = registry.GetType().GetMethod("ForgetConnector")!;

        var attachArgs = new object?[] { first, null };
        Assert.True((bool)tryAttach.Invoke(registry, attachArgs)!);
        Assert.True((bool)attachArgs[1]!);

        attachArgs = [first, null];
        Assert.False((bool)tryAttach.Invoke(registry, attachArgs)!);
        Assert.False((bool)attachArgs[1]!);

        attachArgs = [second, null];
        Assert.True((bool)tryAttach.Invoke(registry, attachArgs)!);
        Assert.False((bool)attachArgs[1]!);

        var detachArgs = new object?[] { "connector-1", null, null };
        Assert.True((bool)tryDetach.Invoke(registry, detachArgs)!);
        Assert.False((bool)detachArgs[2]!);

        detachArgs = ["connector-2", null, null];
        Assert.True((bool)tryDetach.Invoke(registry, detachArgs)!);
        Assert.True((bool)detachArgs[2]!);

        forget.Invoke(registry, ["missing"]);
    }

    [Fact]
    public async Task RedisGossipPublisherSkipsWhenDisabledAndExtensionRegistersServices()
    {
        var connectionFactory = Substitute.For<IRedisRuntimeGossipConnectionFactory>();
        var disabledPublisher = new RedisRuntimeArtifactReadyGossipPublisher(
            connectionFactory,
            Options.Create(new RuntimeGossipOptions { Enabled = false, ChannelName = "runtime" }));

        await disabledPublisher.PublishAsync(new RuntimeArtifactReadyGossipMessage
        {
            ArtifactId = "artifact-1",
            OrchestrationDefinitionKey = "sales.sale.created",
            Version = "1.0.0",
            IngressGeneration = 1,
            OccurredOnUtc = DateTime.UtcNow
        });

        await connectionFactory.DidNotReceiveWithAnyArgs().GetConnectionAsync(default);

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();
        services.AddKrackendOrchestrationsRuntime().AddRedisGossip(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRedisRuntimeGossipConnectionFactory));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeArtifactReadyGossipPublisher));
    }

    [Fact]
    public void SpiderExtensionsValidateNullArgumentsAndRegisterPipelineHooks()
    {
        IPipelineBuilder<TestRequest> nullRequestBuilder = null!;
        IPipelineBuilder<TestRequest, TestResponse> nullResponseBuilder = null!;
        var requestBuilder = Substitute.For<IPipelineBuilder<TestRequest>>();
        var responseBuilder = Substitute.For<IPipelineBuilder<TestRequest, TestResponse>>();

        requestBuilder.OnPreProcess(Arg.Any<Action<Spider.Pipelines.PreProcessing.IPreProcessConfiguration<TestRequest>>>())
            .Returns(requestBuilder);
        requestBuilder.OnPostProcess(Arg.Any<Action<Spider.Pipelines.PostProcessing.IPostProcessConfiguration<TestRequest>>>())
            .Returns(requestBuilder);
        responseBuilder.OnPreProcess(Arg.Any<Action<Spider.Pipelines.PreProcessing.IPreProcessConfiguration<TestRequest>>>())
            .Returns(responseBuilder);
        responseBuilder.OnPostProcess(Arg.Any<Action<Spider.Pipelines.PostProcessing.IPostProcessConfiguration<TestRequest, TestResponse>>>())
            .Returns(responseBuilder);

        Assert.Throws<ArgumentNullException>(() => nullRequestBuilder.UseOrchestration(static request => request));
        Assert.Throws<ArgumentNullException>(() => nullResponseBuilder.UseOrchestration(static response => response));
        Assert.Throws<ArgumentNullException>(() => requestBuilder.UseOrchestration(null!));
        Assert.Throws<ArgumentNullException>(() => responseBuilder.UseOrchestration((Func<TestResponse, object>)null!));

        Assert.Same(requestBuilder, requestBuilder.UseOrchestration(static request => request.Id, "events.test", ""));
        Assert.Same(responseBuilder, responseBuilder.UseOrchestration(static (request, response) => new { request.Id, response.Ok }, "events.test", ""));
    }

    private static object CreateInternal(Assembly assembly, string typeName, params object?[] args)
    {
        var type = assembly.GetType(typeName, throwOnError: true)!;
        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: args,
            culture: null)!;
    }

    private sealed class RecordingProducer : IProducer
    {
        public object? Payload { get; private set; }

        public string? Topic { get; private set; }

        public string? Exchange { get; private set; }

        public string? RoutingKey { get; private set; }

        public SemanticVersion? Version { get; private set; }

        public ValueTask PublishAsync<T>(
            T message,
            string topic,
            SemanticVersion version,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Payload = message;
            Topic = topic;
            Version = version;
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync<T>(
            T message,
            string exchange,
            string routingKey,
            SemanticVersion version,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Payload = message;
            Exchange = exchange;
            RoutingKey = routingKey;
            Version = version;
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
            where T : class
        {
            Payload = message;
            Topic = topic;
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
            where T : class
        {
            Payload = message;
            Topic = topic;
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishRawAsync<T>(
            T message,
            string exchange,
            string routingKey,
            CancellationToken cancellationToken = default)
            where T : class
        {
            Payload = message;
            Exchange = exchange;
            RoutingKey = routingKey;
            return ValueTask.CompletedTask;
        }
    }

    public sealed record TestRequest(string Id);

    public sealed record TestResponse(bool Ok);
}
