using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Client.DependencyInjection;
using Krackend.Sagas.Orchestrations.Client.Errors;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Abstractions.Extensions;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Execution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging;
using Krackend.Sagas.Orchestrations.Runtime.Gossip;
using Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using Spider.Pipelines.Core;
using KrackendId = Krackend.Sagas.Orchestrations.Abstractions.Primitives.Id;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class AdaptersAndContractsTests
{
    [Fact]
    public void ExtensionPrimitiveKeysRenderNullValuesAsEmptyStrings()
    {
        Assert.Equal(string.Empty, new ExtensionKey(null!).ToString());
        Assert.Equal(string.Empty, new CapabilityKey(null!).ToString());
        Assert.Equal(string.Empty, new TaskTypeKey(null!).ToString());
        Assert.Equal(string.Empty, new TriggerTypeKey(null!).ToString());
        Assert.Equal(string.Empty, new NodeTypeKey(null!).ToString());
        Assert.Equal(string.Empty, new ExtensionBundleId(null!).ToString());
        Assert.Equal("contoso.billing", new ExtensionKey("contoso.billing").ToString());
        Assert.Equal("task.invoice", new TaskTypeKey("task.invoice").ToString());
        Assert.Equal("trigger.invoice", new TriggerTypeKey("trigger.invoice").ToString());
        Assert.Equal("node.choice", new NodeTypeKey("node.choice").ToString());
        Assert.Equal("bundle-1", new ExtensionBundleId("bundle-1").ToString());
    }

    [Fact]
    public void ClientExceptionErrorMappingMatchesAssignableTypesAndOptionalPredicates()
    {
        var mapping = new OrchestrationClientExceptionErrorMapping(
            typeof(InvalidOperationException),
            "InvalidOperation",
            exception => exception.Message.Contains("retry", StringComparison.OrdinalIgnoreCase),
            isRetryableCandidate: true);
        var baseMapping = new OrchestrationClientExceptionErrorMapping(typeof(Exception), "AnyException");

        Assert.True(mapping.Matches(new InvalidOperationException("please retry")));
        Assert.False(mapping.Matches(new InvalidOperationException("terminal")));
        Assert.False(mapping.Matches(new ArgumentException("retry")));
        Assert.False(mapping.Matches(null!));
        Assert.True(baseMapping.Matches(new InvalidOperationException("derived")));
        Assert.True(mapping.IsRetryableCandidate);
        Assert.Throws<ArgumentNullException>(() => new OrchestrationClientExceptionErrorMapping(null!, "Error"));
        Assert.Throws<ArgumentException>(() => new OrchestrationClientExceptionErrorMapping(typeof(Exception), " "));
    }

    [Fact]
    public void KrackendPagedResultMaterializesRowsAndTreatsNullAsEmpty()
    {
        var populated = new KrackendPagedResult<string>(2, 4, 7, 3, ["a", "b"]);
        var empty = new KrackendPagedResult<string>(1, 1, 0, 10, null!);

        Assert.Equal(2, populated.PageNumber);
        Assert.Equal(4, populated.TotalPages);
        Assert.Equal(7, populated.TotalRows);
        Assert.Equal(3, populated.PageSize);
        Assert.Equal(["a", "b"], populated.Rows);
        Assert.Empty(empty.Rows);
    }

    [Fact]
    public void RuntimeIngressIdempotencyBuildsExplicitTriggerLegacyAndResponseKeys()
    {
        var explicitKey = RuntimeIngressIdempotency.Build(new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = "sales.sale.created",
            IdempotencyKey = " explicit-key "
        });
        var metadataKey = RuntimeIngressIdempotency.Build(new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = "sales.sale.created",
            Metadata =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = new OrchestrationTriggerMetadata
                {
                    IdempotencyKey = "metadata-key"
                }.ToJson()
            }
        });
        var legacyTriggerKey = RuntimeIngressIdempotency.Build(new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = " sales.sale.created ",
            OrchestrationVersion = " 1.0.0 ",
            CorrelationId = " ",
            Source = new RuntimeTransportDescriptor { MessageId = " " },
            Metadata =
            {
                [OrchestrationMetadataConstants.LegacyTriggerMetadataKey] = new OrchestrationTriggerMetadata
                {
                    EventId = " event-1 ",
                    CorrelationId = " corr-1 "
                }.ToJson()
            }
        });
        var defaultTriggerKey = RuntimeIngressIdempotency.Build(new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Trigger,
            OrchestrationName = "sales.sale.created",
            Source = null!,
            Metadata = null!
        });
        var responseKey = RuntimeIngressIdempotency.Build(new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.TaskResponse,
            OrchestrationName = "sales.sale.created",
            OrchestrationInstanceId = " instance-1 ",
            DispatchId = " dispatch-1 ",
            TaskExecutionId = " task-1 ",
            Attempt = 2,
            Source = null!
        });

        Assert.Equal(" explicit-key ", explicitKey);
        Assert.Equal("metadata-key", metadataKey);
        Assert.Equal("trigger|sales.sale.created|1.0.0|event-1|corr-1", legacyTriggerKey);
        Assert.Equal("trigger|sales.sale.created|-|-|-", defaultTriggerKey);
        Assert.Equal("response|instance-1|dispatch-1|task-1|2|-", responseKey);
        Assert.Throws<ArgumentNullException>(() => RuntimeIngressIdempotency.Build(null!));
        Assert.Throws<InvalidOperationException>(() => RuntimeIngressIdempotency.Build(new RuntimeIngressEnvelope
        {
            Kind = RuntimeIngressKind.Unknown,
            OrchestrationName = "sales.sale.created"
        }));
    }

    [Fact]
    public void PropagationAndTriggerMetadataHandleNullCloneAndJsonFallbacks()
    {
        var nullItems = new OrchestrationPropagationMetadata { Items = null! };
        var populated = new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["json"] = JsonNode.Parse("""{"ok":true}""")!,
                ["nullable"] = null!
            }
        };
        var clone = populated.Clone();
        var nonObjectTrigger = OrchestrationTriggerMetadata.FromJson(JsonValue.Create("not-an-object")!);
        var parsedTrigger = OrchestrationTriggerMetadata.FromJson(new JsonObject
        {
            ["CorrelationId"] = "corr-1",
            ["TraceId"] = null,
            ["EventId"] = JsonValue.Create(42),
            ["EventType"] = "event.created"
        });
        parsedTrigger.AggregateId = "agg-1";
        parsedTrigger.AggregateType = " ";

        Assert.False(nullItems.HasItems);
        Assert.Empty(nullItems.Clone().Items);
        Assert.True(populated.HasItems);
        Assert.NotSame(populated.Items["json"], clone.Items["json"]);
        Assert.True(clone.Items["json"]!["ok"]!.GetValue<bool>());
        Assert.True(clone.Items.ContainsKey("nullable"));
        Assert.Null(nonObjectTrigger.CorrelationId);
        Assert.Equal("corr-1", parsedTrigger.CorrelationId);
        Assert.Equal(string.Empty, parsedTrigger.TraceId);
        Assert.Equal("42", parsedTrigger.EventId);
        Assert.Equal("event.created", parsedTrigger.EventType);
        var json = parsedTrigger.ToJson();
        Assert.Equal("agg-1", json["AggregateId"]!.GetValue<string>());
        Assert.False(json.ContainsKey("AggregateType"));
    }

    [Fact]
    public void ClientPropagationMetadataAccessorClonesSetsNullAndClearsValues()
    {
        var accessorType = typeof(KrackendOrchestrationsClientBuilder).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Metadata.DefaultOrchestrationPropagationMetadataAccessor",
            throwOnError: true)!;
        var accessor = Activator.CreateInstance(accessorType)!;
        var reader = (IOrchestrationPropagationMetadataAccessor)accessor;
        var writer = (IOrchestrationPropagationMetadataSetter)accessor;
        var metadata = new OrchestrationPropagationMetadata();
        metadata.Items["tenant"] = JsonNode.Parse("""{"id":"north"}""")!;

        writer.Set(metadata);
        metadata.Items["tenant"]!["id"] = "changed";

        Assert.Equal("north", reader.Get().Items["tenant"]!["id"]!.GetValue<string>());

        writer.Set(null!);
        Assert.Empty(reader.Get().Items);

        writer.Set(new OrchestrationPropagationMetadata
        {
            Items =
            {
                ["trace"] = JsonValue.Create("trace-1")!
            }
        });
        writer.Clear();

        Assert.Empty(reader.Get().Items);
    }

    [Fact]
    public void IdComparisonAndInternalTriggerRoutesCoverGuardBranches()
    {
        var id = KrackendId.New();
        var address = new OrchestrationReplyAddress();
        var clientAssembly = typeof(KrackendOrchestrationsClientBuilder).Assembly;
        var requestRouteType = clientAssembly
            .GetType("Krackend.Sagas.Orchestrations.Client.Routing.OrchestrationTriggerRoute`1", throwOnError: true)!
            .MakeGenericType(typeof(TestRequest));
        var responseRouteType = clientAssembly
            .GetType("Krackend.Sagas.Orchestrations.Client.Routing.OrchestrationTriggerRoute`2", throwOnError: true)!
            .MakeGenericType(typeof(TestRequest), typeof(TestResponse));
        Func<TestRequest, bool> requestPredicate = static request => !string.IsNullOrWhiteSpace(request.Id);
        Func<TestRequest, object> requestTransform = static request => new { request.Id };
        Func<TestRequest, TestResponse, bool> responsePredicate = static (_, response) => response.Ok;
        Func<TestRequest, TestResponse, object> responseTransform = static (request, response) => new { request.Id, response.Ok };

        var requestRoute = Activator.CreateInstance(requestRouteType, requestPredicate, requestTransform, address)!;
        var responseRoute = Activator.CreateInstance(responseRouteType, responsePredicate, responseTransform, address)!;

        Assert.Equal(0, id.CompareTo(id));
        Assert.Equal(0, id.CompareTo((object)id));
        Assert.Equal(1, id.CompareTo(null));
        Assert.Throws<ArgumentException>(() => id.CompareTo("not-an-id"));
        Assert.Same(address, requestRouteType.GetProperty("Address")!.GetValue(requestRoute));
        Assert.Same(address, responseRouteType.GetProperty("Address")!.GetValue(responseRoute));
        AssertWrappedArgumentNull("predicate", () => Activator.CreateInstance(requestRouteType, null!, requestTransform, address));
        AssertWrappedArgumentNull("transform", () => Activator.CreateInstance(requestRouteType, requestPredicate, null!, address));
        AssertWrappedArgumentNull("predicate", () => Activator.CreateInstance(responseRouteType, null!, responseTransform, address));
        AssertWrappedArgumentNull("transform", () => Activator.CreateInstance(responseRouteType, responsePredicate, null!, address));
    }

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
    public async Task ClientPigeonPublisherValidatesConstructorArgumentsAndHandlesNullPayloadWithoutResultMetadata()
    {
        var assembly = typeof(Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        var producer = new RecordingProducer();
        var serializer = CreateInternal(
            assembly,
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.DefaultMessagingReplyAddressSettingsSerializer");
        var resultMetadataAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        resultMetadataAccessor.Get().Returns(_ => null!);

        AssertWrappedArgumentNull(
            "producer",
            () => CreateInternal(
                assembly,
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.PigeonOrchestrationClientPublisher",
                null!,
                serializer,
                resultMetadataAccessor));
        AssertWrappedArgumentNull(
            "settingsSerializer",
            () => CreateInternal(
                assembly,
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.PigeonOrchestrationClientPublisher",
                producer,
                null!,
                resultMetadataAccessor));
        AssertWrappedArgumentNull(
            "resultMetadataAccessor",
            () => CreateInternal(
                assembly,
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.PigeonOrchestrationClientPublisher",
                producer,
                serializer,
                null!));

        var publisher = (IOrchestrationClientPublisher)CreateInternal(
            assembly,
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.PigeonOrchestrationClientPublisher",
            producer,
            serializer,
            resultMetadataAccessor);
        var address = new OrchestrationReplyAddress
        {
            Transport = OrchestrationTransportNames.Messaging,
            SettingsPayload = JsonSerializer.Serialize(new MessagingReplyAddressSettings
            {
                Topic = "orchestrations.sales.null",
                Version = "1.0.0"
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

        await publisher.PublishAsync(null!, address);

        Assert.IsType<System.Text.Json.Nodes.JsonObject>(producer.Payload);
        Assert.Equal("orchestrations.sales.null", producer.Topic);
        resultMetadataAccessor.Received(1).Get();
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
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, configuration, _ => { }, null!));

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
            descriptor.ServiceType.Name == "IConsumeExecutionInterceptor" &&
            descriptor.ImplementationType?.Name == "KrackendClientConsumeInterceptor");
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.Name == "IPublishInterceptor" &&
            descriptor.ImplementationType?.Name == "KrackendClientPublishInterceptor");
    }

    [Fact]
    public void ClientCoreExtensionValidatesServiceCollectionAndConfigureCallback()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddKrackendOrchestrationsClient());

        services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddKrackendOrchestrationsClient(null!));
    }

    [Fact]
    public void ClientPigeonExtensionExposesPigeonServiceBuilderConfiguration()
    {
        var services = new ServiceCollection();
        var builder = services.AddKrackendOrchestrationsClient();
        var configuration = CreatePigeonConfiguration();

        Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(
            builder,
            configuration,
            _ => { },
            pigeon => pigeon.ConfigureJsonOptions(options =>
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase));

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<ISerializer>();
        var json = serializer.Serialize(new PigeonJsonOptionsProbe("ready"));

        Assert.Contains("\"sampleValue\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SampleValue\"", json, StringComparison.Ordinal);
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
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, configuration, _ => { }, null!));

        var returned = Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(builder, configuration);

        Assert.Same(builder, returned);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMessagingDispatchAdapter));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging.IMessagingIngressAdapter));
    }

    [Fact]
    public void RuntimeBuilderRegistersExternalExecutionSandboxProvider()
    {
        KrackendOrchestrationsRuntimeBuilder nullBuilder = null!;
        var services = new ServiceCollection();
        var builder = services.AddKrackendOrchestrationsRuntime();

        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Runtime.DependencyInjection.ServiceCollectionExtensions
                .AddExecutionSandboxProvider<ContractExecutionSandboxProvider>(nullBuilder));

        var returned = builder.AddExecutionSandboxProvider<ContractExecutionSandboxProvider>();

        Assert.Same(builder, returned);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IExecutionSandboxProvider) &&
            descriptor.ImplementationType == typeof(ContractExecutionSandboxProvider));

        using var provider = services.BuildServiceProvider();
        var executionProviders = provider.GetServices<IExecutionSandboxProvider>().ToArray();

        Assert.Contains(executionProviders, executionProvider =>
            executionProvider.ProviderKey == ContractExecutionSandboxProvider.Provider);
    }

    [Fact]
    public void RuntimePigeonExtensionExposesPigeonServiceBuilderConfiguration()
    {
        var services = new ServiceCollection();
        var builder = services.AddKrackendOrchestrationsRuntime();
        var configuration = CreatePigeonConfiguration();

        Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions.AddPigeon(
            builder,
            configuration,
            _ => { },
            pigeon => pigeon.ConfigureJsonOptions(options =>
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase));

        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<ISerializer>();
        var json = serializer.Serialize(new PigeonJsonOptionsProbe("ready"));

        Assert.Contains("\"sampleValue\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SampleValue\"", json, StringComparison.Ordinal);
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

    private static void AssertWrappedArgumentNull(string paramName, Action action)
    {
        var exception = Assert.Throws<TargetInvocationException>(action);
        var argumentException = Assert.IsType<ArgumentNullException>(exception.InnerException);
        Assert.Equal(paramName, argumentException.ParamName);
    }

    private static IConfiguration CreatePigeonConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pigeon:Domain"] = "Krackend.Tests",
                ["Pigeon:MessageBrokers:RabbitMq:Url"] = "amqp://guest:guest@localhost:5672"
            })
            .Build();

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

    private sealed record PigeonJsonOptionsProbe(string SampleValue);

    private sealed class ContractExecutionSandboxProvider : IExecutionSandboxProvider
    {
        public const string Provider = "contract-sandbox";

        public string ProviderKey => Provider;

        public string ExecutionMode => "sandbox";

        public bool IsSandbox => true;

        public Task DispatchAsync(ExecutionEnvelope envelope, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
