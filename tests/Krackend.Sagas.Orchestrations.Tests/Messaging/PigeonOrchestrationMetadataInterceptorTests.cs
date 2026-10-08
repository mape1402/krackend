namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using NSubstitute;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Producing;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class PigeonOrchestrationMetadataInterceptorTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static IEnumerable<object[]> PropagationMapperTypes =>
    [
        [
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon",
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.PigeonPropagationMetadataMapper"
        ],
        [
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon",
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.PigeonPropagationMetadataMapper"
        ]
    ];

    [Theory]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor")]
    public async Task PublishInterceptorsAttachOrchestrationMetadataWithoutChangingPayload(string assemblyName, string typeName)
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            CorrelationId = "correlation-1"
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded",
            ServiceName = "inventories"
        };
        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        messageAccessor.Get().Returns(messageMetadata);
        resultAccessor.Get().Returns(resultMetadata);
        var interceptor = CreatePublishInterceptor(assemblyName, typeName, messageAccessor, resultAccessor);
        var context = new PublishContext();

        await interceptor.Intercept(context, CancellationToken.None);

        var metadata = GetPublishMetadata(context);
        Assert.Same(messageMetadata, metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey]);
        Assert.Same(resultMetadata, metadata[OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey]);
    }

    [Theory]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor")]
    public async Task PublishInterceptorsSkipEmptyMessageMetadataButKeepExecutionResult(string assemblyName, string typeName)
    {
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = "InventoryUnavailable"
        };
        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        messageAccessor.Get().Returns(new OrchestrationMessageMetadata());
        resultAccessor.Get().Returns(resultMetadata);
        var interceptor = CreatePublishInterceptor(assemblyName, typeName, messageAccessor, resultAccessor);
        var context = new PublishContext();

        await interceptor.Intercept(context, CancellationToken.None);

        var metadata = GetPublishMetadata(context);
        Assert.False(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationMessageMetadataKey));
        Assert.Same(resultMetadata, metadata[OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey]);
    }

    [Theory]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor", "saga")]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor", "instance")]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor", "correlation")]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor", "task")]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor", "reply")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor", "saga")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor", "instance")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor", "correlation")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor", "task")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor", "reply")]
    public async Task PublishInterceptorsAttachMessageMetadataWhenAnySupportedFieldIsPresent(
        string assemblyName,
        string typeName,
        string field)
    {
        var messageMetadata = new OrchestrationMessageMetadata();
        switch (field)
        {
            case "saga":
                messageMetadata.SagaId = "saga-1";
                break;
            case "instance":
                messageMetadata.OrchestrationInstanceId = "instance-1";
                break;
            case "correlation":
                messageMetadata.CorrelationId = "correlation-1";
                break;
            case "task":
                messageMetadata.TaskExecutionId = "task-1";
                break;
            case "reply":
                messageMetadata.ReplyAddress = new OrchestrationReplyAddress
                {
                    Transport = "pigeon",
                    SettingsPayload = """{"queue":"reply.queue"}"""
                };
                break;
        }

        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        messageAccessor.Get().Returns(messageMetadata);
        resultAccessor.Get().Returns(_ => null!);
        var interceptor = CreatePublishInterceptor(assemblyName, typeName, messageAccessor, resultAccessor);
        var context = new PublishContext();

        await interceptor.Intercept(context, CancellationToken.None);

        var metadata = GetPublishMetadata(context);
        Assert.Same(messageMetadata, metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey]);
    }

    [Theory]
    [InlineData("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendPublishInterceptor")]
    [InlineData("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon", "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor")]
    public async Task PublishInterceptorsAttachPropagationMetadataAsIndividualItemsOnly(string assemblyName, string typeName)
    {
        var propagationMetadata = new OrchestrationPropagationMetadata
        {
            Items =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = JsonNode.Parse("""{"CorrelationId":"corr-1"}"""),
                [OrchestrationMetadataConstants.OriginMetadataKey] = JsonNode.Parse("""{"SagaId":"saga-1"}"""),
                ["audit.context"] = JsonNode.Parse("""{"requestId":"req-1","attempt":2}"""),
                ["security.context"] = JsonNode.Parse("""{"tenant":"north"}""")
            }
        };
        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        var propagationAccessor = Substitute.For<IOrchestrationPropagationMetadataAccessor>();
        messageAccessor.Get().Returns(new OrchestrationMessageMetadata());
        resultAccessor.Get().Returns(_ => null!);
        propagationAccessor.Get().Returns(propagationMetadata);
        var interceptor = CreatePublishInterceptor(assemblyName, typeName, messageAccessor, resultAccessor, propagationAccessor);
        var context = new PublishContext();

        await interceptor.Intercept(context, CancellationToken.None);

        var metadata = GetPublishMetadata(context);
        Assert.False(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey));
        Assert.Equal("corr-1", ((JsonNode)metadata[OrchestrationMetadataConstants.TriggerMetadataKey])!["CorrelationId"]!.GetValue<string>());
        Assert.Equal("saga-1", ((JsonNode)metadata[OrchestrationMetadataConstants.OriginMetadataKey])!["SagaId"]!.GetValue<string>());
        Assert.Equal("req-1", ((JsonNode)metadata["audit.context"])!["requestId"]!.GetValue<string>());
        Assert.Equal("north", ((JsonNode)metadata["security.context"])!["tenant"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(PropagationMapperTypes))]
    public void PropagationMappersAttachSkipNullEmptyAndReservedMetadata(string assemblyName, string typeName)
    {
        var mapper = CreatePropagationMapper(assemblyName, typeName);
        var context = new PublishContext();
        var propagationMetadata = new OrchestrationPropagationMetadata
        {
            Items =
            {
                [OrchestrationMetadataConstants.TriggerMetadataKey] = JsonNode.Parse("""{"CorrelationId":"corr-2"}"""),
                [OrchestrationMetadataConstants.OriginMetadataKey] = JsonNode.Parse("""{"SagaId":"saga-2"}"""),
                ["audit.context"] = JsonNode.Parse("""{"requestId":"req-4"}"""),
                ["Krackend.Sagas.Orchestrations.Internal"] = JsonValue.Create("reserved")
            }
        };

        AttachPropagationMetadata(mapper, context, null);
        AttachPropagationMetadata(mapper, context, new OrchestrationPropagationMetadata());
        AttachPropagationMetadata(mapper, context, propagationMetadata);

        var metadata = GetPublishMetadata(context);
        Assert.False(metadata.ContainsKey(OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey));
        Assert.False(metadata.ContainsKey("Krackend.Sagas.Orchestrations.Internal"));
        Assert.Equal("corr-2", ((JsonNode)metadata[OrchestrationMetadataConstants.TriggerMetadataKey])!["CorrelationId"]!.GetValue<string>());
        Assert.Equal("saga-2", ((JsonNode)metadata[OrchestrationMetadataConstants.OriginMetadataKey])!["SagaId"]!.GetValue<string>());
        Assert.Equal("req-4", ((JsonNode)metadata["audit.context"])!["requestId"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(PropagationMapperTypes))]
    public void PropagationMappersCaptureEnvelopeObjectAndRawMetadata(string assemblyName, string typeName)
    {
        var mapper = CreatePropagationMapper(assemblyName, typeName);
        var context = new ConsumeContext
        {
            RawMetadata = new Dictionary<string, string>
            {
                ["audit.context"] = """{"requestId":"raw-ignored"}""",
                ["raw.json"] = """{"fromRaw":true}""",
                ["raw.text"] = "plain raw",
                ["Krackend.Sagas.Orchestrations.Raw"] = "reserved",
                [" "] = "blank"
            }
        };
        using var jsonDocument = JsonDocument.Parse("""{"fromElement":true}""");
        SetConsumeMetadataItem(
            context,
            OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey,
            new Dictionary<string, JsonNode>
            {
                ["audit.context"] = JsonNode.Parse("""{"requestId":"dict-envelope"}""")!,
                [OrchestrationMetadataConstants.OriginMetadataKey] = JsonNode.Parse("""{"SagaId":"saga-envelope"}""")!,
                [" "] = JsonValue.Create("blank"),
                ["Krackend.Sagas.Orchestrations.Envelope"] = JsonValue.Create("reserved")
            });
        SetConsumeMetadataItem(context, "object.node", JsonNode.Parse("""{"fromNode":true}""")!);
        SetConsumeMetadataItem(context, "object.element", jsonDocument.RootElement.Clone());
        SetConsumeMetadataItem(context, "object.jsonString", """{"fromString":true}""");
        SetConsumeMetadataItem(context, "object.blankString", " ");
        SetConsumeMetadataItem(context, "object.text", "hello");
        SetConsumeMetadataItem(context, "object.serialized", new { Count = 7 });
        SetConsumeMetadataItem(context, "object.throwing", new ThrowingMetadataValue());
        SetConsumeMetadataItem(context, OrchestrationMetadataConstants.TriggerMetadataKey, JsonNode.Parse("""{"CorrelationId":"trigger-1"}""")!);
        SetConsumeMetadataItem(context, "Krackend.Sagas.Orchestrations.Object", new { ignored = true });
        SetConsumeMetadataItem(context, " ", new { ignored = true });

        var metadata = CapturePropagationMetadata(mapper, context);

        Assert.Equal("dict-envelope", metadata.Items["audit.context"]!["requestId"]!.GetValue<string>());
        Assert.Equal("saga-envelope", metadata.Items[OrchestrationMetadataConstants.OriginMetadataKey]!["SagaId"]!.GetValue<string>());
        Assert.True(metadata.Items["object.node"]!["fromNode"]!.GetValue<bool>());
        Assert.True(metadata.Items["object.element"]!["fromElement"]!.GetValue<bool>());
        Assert.True(metadata.Items["object.jsonString"]!["fromString"]!.GetValue<bool>());
        Assert.Equal(" ", metadata.Items["object.blankString"]!.GetValue<string>());
        Assert.Equal("hello", metadata.Items["object.text"]!.GetValue<string>());
        Assert.Equal(7, metadata.Items["object.serialized"]!["count"]!.GetValue<int>());
        Assert.Equal("fallback", metadata.Items["object.throwing"]!.GetValue<string>());
        Assert.Equal("trigger-1", metadata.Items[OrchestrationMetadataConstants.TriggerMetadataKey]!["CorrelationId"]!.GetValue<string>());
        Assert.True(metadata.Items["raw.json"]!["fromRaw"]!.GetValue<bool>());
        Assert.Equal("plain raw", metadata.Items["raw.text"]!.GetValue<string>());
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Envelope", metadata.Items.Keys);
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Object", metadata.Items.Keys);
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Raw", metadata.Items.Keys);
        Assert.DoesNotContain(" ", metadata.Items.Keys);
    }

    [Theory]
    [MemberData(nameof(PropagationMapperTypes))]
    public void PropagationMappersHandleEmptyContextsAndNullValues(string assemblyName, string typeName)
    {
        var mapper = CreatePropagationMapper(assemblyName, typeName);
        var emptyContext = new ConsumeContext { RawMetadata = null };
        var nullObjectMetadataContext = new ConsumeContext { RawMetadata = null };
        var unusualObjectMetadataContext = new ConsumeContext
        {
            RawMetadata = new Dictionary<string, string>
            {
                ["duplicate"] = "raw-ignored",
                ["raw-only"] = "raw-kept"
            }
        };
        SetConsumeMetadataStorage(nullObjectMetadataContext, null);
        SetConsumeMetadataItem(unusualObjectMetadataContext, "", "blank");
        SetConsumeMetadataItem(unusualObjectMetadataContext, "Krackend.Sagas.Orchestrations.Reserved", "reserved");
        SetConsumeMetadataItem(unusualObjectMetadataContext, "duplicate", JsonValue.Create("object-kept")!);
        SetConsumeMetadataItem(unusualObjectMetadataContext, "object-only", "object-kept");

        var captured = CapturePropagationMetadata(mapper, emptyContext);
        var capturedWithoutObjectMetadata = CapturePropagationMetadata(mapper, nullObjectMetadataContext);
        var capturedUnusualObjectMetadata = CapturePropagationMetadata(mapper, unusualObjectMetadataContext);
        var convertedNull = InvokeConvertValue(mapper, null);
        var target = new OrchestrationPropagationMetadata();
        InvokeMergeDictionary(mapper, new OrchestrationPropagationMetadata(), null);
        InvokeMergePropagation(mapper, target, null);
        InvokeMergeDictionary(mapper, target, new Dictionary<string, JsonNode>
        {
            ["nullable"] = null!
        });

        Assert.Empty(captured.Items);
        Assert.Empty(capturedWithoutObjectMetadata.Items);
        Assert.Equal("object-kept", capturedUnusualObjectMetadata.Items["duplicate"]!.GetValue<string>());
        Assert.Equal("object-kept", capturedUnusualObjectMetadata.Items["object-only"]!.GetValue<string>());
        Assert.Equal("raw-kept", capturedUnusualObjectMetadata.Items["raw-only"]!.GetValue<string>());
        Assert.DoesNotContain("Krackend.Sagas.Orchestrations.Reserved", capturedUnusualObjectMetadata.Items.Keys);
        Assert.Null(convertedNull);
        Assert.True(target.Items.ContainsKey("nullable"));
        Assert.Null(target.Items["nullable"]);
    }

    [Theory]
    [MemberData(nameof(PropagationMapperTypes))]
    public void PropagationMappersAttachNullItemValues(string assemblyName, string typeName)
    {
        var mapper = CreatePropagationMapper(assemblyName, typeName);
        var context = new PublishContext();
        var propagationMetadata = new OrchestrationPropagationMetadata();
        propagationMetadata.Items["nullable"] = null!;

        AttachPropagationMetadata(mapper, context, propagationMetadata);

        var metadata = GetPublishMetadata(context);
        Assert.True(metadata.ContainsKey("nullable"));
        Assert.Null(metadata["nullable"]);
    }

    [Fact]
    public async Task ClientPublishInterceptorAttachesSagaIdAndReplyAddressMetadata()
    {
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();

        foreach (var messageMetadata in new[]
        {
            new OrchestrationMessageMetadata { SagaId = "saga-1" },
            new OrchestrationMessageMetadata { OrchestrationInstanceId = "instance-1" },
            new OrchestrationMessageMetadata { CorrelationId = "correlation-1" },
            new OrchestrationMessageMetadata { TaskExecutionId = "task-1" },
            new OrchestrationMessageMetadata { ReplyAddress = new OrchestrationReplyAddress { Transport = "messaging" } }
        })
        {
            var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
            messageAccessor.Get().Returns(messageMetadata);
            resultAccessor.Get().Returns(_ => null!);
            var interceptor = CreatePublishInterceptor(
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon",
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor",
                messageAccessor,
                resultAccessor);
            var context = new PublishContext();

            await interceptor.Intercept(context, CancellationToken.None);

            var metadata = GetPublishMetadata(context);
            Assert.Same(messageMetadata, metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey]);
        }

        var emptyMessageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        emptyMessageAccessor.Get().Returns(_ => null!);
        resultAccessor.Get().Returns(_ => null!);
        var emptyInterceptor = CreatePublishInterceptor(
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon",
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor",
            emptyMessageAccessor,
            resultAccessor);
        var emptyContext = new PublishContext();

        await emptyInterceptor.Intercept(emptyContext, CancellationToken.None);

        Assert.Empty(GetPublishMetadata(emptyContext));
    }

    [Fact]
    public async Task ClientInterceptorsValidateConstructorArgumentsAndExecutionDelegate()
    {
        var messageAccessor = Substitute.For<IOrchestrationMessageMetadataAccessor>();
        var resultAccessor = Substitute.For<IOrchestrationExecutionResultMetadataAccessor>();
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();

        AssertWrappedArgumentNull(
            "messageMetadataAccessor",
            () => CreatePublishInterceptor(
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon",
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor",
                null!,
                resultAccessor));
        AssertWrappedArgumentNull(
            "resultMetadataAccessor",
            () => CreatePublishInterceptor(
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon",
                "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientPublishInterceptor",
                messageAccessor,
                null!));
        AssertWrappedArgumentNull(
            "metadataSetter",
            () => CreateClientConsumeInterceptor(null!, resultSetter));
        AssertWrappedArgumentNull(
            "resultMetadataSetter",
            () => CreateClientConsumeInterceptor(messageSetter, null!));

        var executionInterceptor = CreateClientConsumeExecutionInterceptor(messageSetter, resultSetter);
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await executionInterceptor.InvokeAsync(new ConsumeContext(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task RuntimeConsumeInterceptorReadsMetadataAndClearsMissingExecutionResult()
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1"
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded"
        };
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var interceptor = CreateConsumeInterceptor(messageSetter, resultSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(context, messageMetadata, resultMetadata);

        await interceptor.Intercept(context, CancellationToken.None);

        messageSetter.Received(1).Set(messageMetadata);
        resultSetter.Received(1).Clear();
        resultSetter.Received(1).Set(resultMetadata);

        messageSetter.ClearReceivedCalls();
        resultSetter.ClearReceivedCalls();
        await interceptor.Intercept(new ConsumeContext(), CancellationToken.None);

        messageSetter.Received(1).Set(Arg.Is<OrchestrationMessageMetadata>(metadata =>
            string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId) &&
            string.IsNullOrWhiteSpace(metadata.TaskExecutionId)));
        resultSetter.Received(2).Clear();
        resultSetter.DidNotReceive().Set(Arg.Any<OrchestrationExecutionResultMetadata>());
    }

    [Fact]
    public async Task RuntimeConsumeInterceptorReadsPropagationMetadataFromEnvelopeAndObjectMetadata()
    {
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var propagationSetter = Substitute.For<IOrchestrationPropagationMetadataSetter>();
        var interceptor = CreateConsumeInterceptor(messageSetter, resultSetter, propagationSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(
            context,
            new OrchestrationMessageMetadata(),
            new OrchestrationExecutionResultMetadata(),
            new OrchestrationPropagationMetadata
            {
                Items =
                {
                    ["audit.context"] = JsonNode.Parse("""{"requestId":"req-2"}""")
                }
            });
        SetConsumeMetadataItem(context, "security.context", new { tenant = "north" });

        await interceptor.Intercept(context, CancellationToken.None);

        propagationSetter.Received(1).Set(Arg.Is<OrchestrationPropagationMetadata>(metadata =>
            metadata.Items["audit.context"]!["requestId"]!.GetValue<string>() == "req-2" &&
            metadata.Items["security.context"]!["tenant"]!.GetValue<string>() == "north"));
    }

    [Fact]
    public async Task ClientConsumeInterceptorReadsMessageMetadataAndClearsExecutionResult()
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            CorrelationId = "correlation-1"
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded"
        };
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var interceptor = CreateClientConsumeInterceptor(messageSetter, resultSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(context, messageMetadata, resultMetadata);

        await interceptor.Intercept(context, CancellationToken.None);

        messageSetter.Received(1).Set(messageMetadata);
        resultSetter.Received(1).Clear();
        resultSetter.DidNotReceive().Set(Arg.Any<OrchestrationExecutionResultMetadata>());

        messageSetter.ClearReceivedCalls();
        resultSetter.ClearReceivedCalls();
        await interceptor.Intercept(new ConsumeContext(), CancellationToken.None);

        messageSetter.Received(1).Set(Arg.Is<OrchestrationMessageMetadata>(metadata =>
            string.IsNullOrWhiteSpace(metadata.OrchestrationInstanceId) &&
            string.IsNullOrWhiteSpace(metadata.TaskExecutionId)));
        resultSetter.Received(1).Clear();
    }

    [Fact]
    public async Task ClientConsumeInterceptorReadsPropagationMetadata()
    {
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var propagationSetter = Substitute.For<IOrchestrationPropagationMetadataSetter>();
        var interceptor = CreateClientConsumeInterceptor(messageSetter, resultSetter, propagationSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(
            context,
            new OrchestrationMessageMetadata(),
            new OrchestrationExecutionResultMetadata(),
            new OrchestrationPropagationMetadata
            {
                Items =
                {
                    ["audit.context"] = JsonNode.Parse("""{"requestId":"req-3"}""")
                }
            });

        await interceptor.Intercept(context, CancellationToken.None);

        propagationSetter.Received(1).Set(Arg.Is<OrchestrationPropagationMetadata>(metadata =>
            metadata.Items["audit.context"]!["requestId"]!.GetValue<string>() == "req-3"));
    }

    [Fact]
    public async Task ClientConsumeExecutionInterceptorRestoresMetadataBeforeHandlerExecution()
    {
        var messageMetadata = new OrchestrationMessageMetadata
        {
            OrchestrationInstanceId = "instance-1",
            TaskExecutionId = "task-1",
            CorrelationId = "correlation-1",
            ReplyAddress = new OrchestrationReplyAddress
            {
                Transport = OrchestrationTransportNames.Messaging,
                SettingsPayload = """{"topic":"orchestrations.sales.sale.created","version":"1.0.2"}"""
            }
        };
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded"
        };
        var messageSetter = Substitute.For<IOrchestrationMessageMetadataSetter>();
        var resultSetter = Substitute.For<IOrchestrationExecutionResultMetadataSetter>();
        var interceptor = CreateClientConsumeExecutionInterceptor(messageSetter, resultSetter);
        var context = new ConsumeContext();
        SetConsumeMetadata(context, messageMetadata, resultMetadata);
        var nextInvoked = false;

        await interceptor.InvokeAsync(
            context,
            (nextContext, _) =>
            {
                nextInvoked = ReferenceEquals(context, nextContext);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(nextInvoked);
        messageSetter.Received(1).Set(messageMetadata);
        resultSetter.Received(1).Clear();
        resultSetter.DidNotReceive().Set(Arg.Any<OrchestrationExecutionResultMetadata>());
    }

    private static Pigeon.Messaging.Producing.IPublishInterceptor CreatePublishInterceptor(
        string assemblyName,
        string typeName,
        IOrchestrationMessageMetadataAccessor messageAccessor,
        IOrchestrationExecutionResultMetadataAccessor resultAccessor,
        IOrchestrationPropagationMetadataAccessor? propagationAccessor = null)
    {
        var assembly = Assembly.Load(assemblyName);
        var type = assembly.GetType(typeName, throwOnError: true)!;
        var args = propagationAccessor is null
            ? new object[] { messageAccessor, resultAccessor }
            : [messageAccessor, resultAccessor, propagationAccessor];

        return (Pigeon.Messaging.Producing.IPublishInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: args,
            culture: null)!;
    }

    private static object CreatePropagationMapper(string assemblyName, string typeName)
    {
        var assembly = Assembly.Load(assemblyName);
        var type = assembly.GetType(typeName, throwOnError: true)!;
        return Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: [],
            culture: null)!;
    }

    private static void AttachPropagationMetadata(
        object mapper,
        PublishContext context,
        OrchestrationPropagationMetadata? metadata)
    {
        var method = mapper.GetType().GetMethod("Attach", InstanceFlags)!;
        method.Invoke(mapper, [context, metadata]);
    }

    private static OrchestrationPropagationMetadata CapturePropagationMetadata(object mapper, ConsumeContext context)
    {
        var method = mapper.GetType().GetMethod("Capture", InstanceFlags)!;
        return (OrchestrationPropagationMetadata)method.Invoke(mapper, [context])!;
    }

    private static JsonNode? InvokeConvertValue(object mapper, object? value)
    {
        var method = mapper.GetType().GetMethod("ConvertValue", InstanceFlags | BindingFlags.Static)!;
        return (JsonNode?)method.Invoke(null, [value]);
    }

    private static void InvokeMergeDictionary(
        object mapper,
        OrchestrationPropagationMetadata target,
        IDictionary<string, JsonNode>? source)
    {
        var method = mapper.GetType()
            .GetMethods(InstanceFlags | BindingFlags.Static)
            .Single(candidate =>
            {
                if (candidate.Name != "Merge")
                {
                    return false;
                }

                var parameters = candidate.GetParameters();
                return parameters.Length == 2 &&
                    parameters[1].ParameterType == typeof(IDictionary<string, JsonNode>);
            });
        method.Invoke(null, [target, source]);
    }

    private static void InvokeMergePropagation(
        object mapper,
        OrchestrationPropagationMetadata target,
        OrchestrationPropagationMetadata? source)
    {
        var method = mapper.GetType()
            .GetMethods(InstanceFlags | BindingFlags.Static)
            .Single(candidate =>
            {
                if (candidate.Name != "Merge")
                {
                    return false;
                }

                var parameters = candidate.GetParameters();
                return parameters.Length == 2 &&
                    parameters[1].ParameterType == typeof(OrchestrationPropagationMetadata);
            });
        method.Invoke(null, [target, source]);
    }

    private static void AssertWrappedArgumentNull(string paramName, Action action)
    {
        var exception = Assert.Throws<TargetInvocationException>(action);
        var argumentException = Assert.IsType<ArgumentNullException>(exception.InnerException);
        Assert.Equal(paramName, argumentException.ParamName);
    }

    private static Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor CreateConsumeInterceptor(
        IOrchestrationMessageMetadataSetter messageSetter,
        IOrchestrationExecutionResultMetadataSetter resultSetter,
        IOrchestrationPropagationMetadataSetter? propagationSetter = null)
    {
        var assembly = Assembly.Load("Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon");
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.Interceptors.KrackendConsumeInterceptor",
            throwOnError: true)!;
        var args = propagationSetter is null
            ? new object[] { messageSetter, resultSetter }
            : [messageSetter, resultSetter, propagationSetter];

        return (Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: args,
            culture: null)!;
    }

    private static Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor CreateClientConsumeInterceptor(
        IOrchestrationMessageMetadataSetter messageSetter,
        IOrchestrationExecutionResultMetadataSetter resultSetter,
        IOrchestrationPropagationMetadataSetter? propagationSetter = null)
    {
        var assembly = Assembly.Load("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon");
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientConsumeInterceptor",
            throwOnError: true)!;
        var args = propagationSetter is null
            ? new object[] { messageSetter, resultSetter }
            : [messageSetter, resultSetter, propagationSetter];

        return (Pigeon.Messaging.Consuming.Dispatching.IConsumeInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: args,
            culture: null)!;
    }

    private static Pigeon.Messaging.Consuming.Dispatching.IConsumeExecutionInterceptor CreateClientConsumeExecutionInterceptor(
        IOrchestrationMessageMetadataSetter messageSetter,
        IOrchestrationExecutionResultMetadataSetter resultSetter)
    {
        var assembly = Assembly.Load("Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon");
        var type = assembly.GetType(
            "Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.KrackendClientConsumeInterceptor",
            throwOnError: true)!;
        return (Pigeon.Messaging.Consuming.Dispatching.IConsumeExecutionInterceptor)Activator.CreateInstance(
            type,
            InstanceFlags,
            binder: null,
            args: [messageSetter, resultSetter],
            culture: null)!;
    }

    private static IReadOnlyDictionary<string, object> GetPublishMetadata(PublishContext context)
    {
        var method = typeof(PublishContext).GetMethod("GetMetadata", InstanceFlags)!;
        return (IReadOnlyDictionary<string, object>)method.Invoke(context, [])!;
    }

    private static void SetConsumeMetadata(
        ConsumeContext context,
        OrchestrationMessageMetadata messageMetadata,
        OrchestrationExecutionResultMetadata resultMetadata,
        OrchestrationPropagationMetadata? propagationMetadata = null)
    {
        var field = typeof(ConsumeContext).GetField("_metadata", InstanceFlags)!;
        var metadata = (ConcurrentDictionary<string, object>)field.GetValue(context)!;
        metadata[OrchestrationMetadataConstants.OrchestrationMessageMetadataKey] = messageMetadata;
        metadata[OrchestrationMetadataConstants.OrchestrationExecutionResultMetadataKey] = resultMetadata;
        if (propagationMetadata is not null)
        {
            metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] = propagationMetadata;
        }
    }

    private static void SetConsumeMetadataItem(
        ConsumeContext context,
        string key,
        object value)
    {
        var field = typeof(ConsumeContext).GetField("_metadata", InstanceFlags)!;
        var metadata = (ConcurrentDictionary<string, object>)field.GetValue(context)!;
        metadata[key] = value;
    }

    private static void SetConsumeMetadataStorage(
        ConsumeContext context,
        object? value)
    {
        var field = typeof(ConsumeContext).GetField("_metadata", InstanceFlags)!;
        field.SetValue(context, value);
    }

    private sealed class ThrowingMetadataValue
    {
        public string Broken => throw new InvalidOperationException("boom");

        public override string ToString() => "fallback";
    }

}
