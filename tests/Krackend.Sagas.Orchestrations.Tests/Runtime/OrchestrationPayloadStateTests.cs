namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Metadata;
using System.Text.Json.Nodes;

public sealed class OrchestrationPayloadStateTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void CreateInitialPayloadWrapsTriggerPayloadWithoutMutatingBusinessPayload()
    {
        var state = CreatePayloadState();
        var businessPayload = JsonNode.Parse("""{"saleId":"sale-1","total":25}""")!;

        var snapshot = Invoke<JsonNode>(state, "CreateInitialPayload", businessPayload);
        businessPayload["saleId"] = "changed";

        Assert.Equal("sale-1", snapshot["trigger"]!["payload"]!["saleId"]!.GetValue<string>());
        Assert.NotNull(snapshot["stages"]);
        Assert.NotNull(snapshot["variables"]);
    }

    [Fact]
    public void CreateInitialPayloadAcceptsNullTriggerPayload()
    {
        var state = CreatePayloadState();

        var snapshot = Invoke<JsonNode>(state, "CreateInitialPayload", (object?)null);

        Assert.NotNull(snapshot["trigger"]);
        Assert.True(snapshot.AsObject().ContainsKey("stages"));
        Assert.True(snapshot.AsObject().ContainsKey("variables"));
    }

    [Fact]
    public void GetDispatchPayloadUsesOriginalTriggerPayloadWhenSnapshotIsStructured()
    {
        var state = CreatePayloadState();
        var instance = Instance(JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}},"stages":{},"variables":{}}"""));
        var signal = JsonNode.Parse("""{"saleId":"signal"}""")!;

        var dispatchPayload = Invoke<JsonNode>(state, "GetDispatchPayload", instance, signal);
        dispatchPayload["saleId"] = "changed";

        Assert.Equal("changed", dispatchPayload["saleId"]!.GetValue<string>());
        Assert.Equal("sale-1", instance.SnapshotPayload!["trigger"]!["payload"]!["saleId"]!.GetValue<string>());
    }

    [Fact]
    public void GetDispatchPayloadFallsBackToSnapshotOrSignalWhenTriggerEnvelopeIsMissing()
    {
        var state = CreatePayloadState();
        var instanceWithLegacySnapshot = Instance(JsonNode.Parse("""{"legacy":true}"""));
        var instanceWithoutSnapshot = Instance();
        var signal = JsonNode.Parse("""{"saleId":"signal"}""")!;

        var legacyPayload = Invoke<JsonNode>(state, "GetDispatchPayload", instanceWithLegacySnapshot, signal);
        var signalPayload = Invoke<JsonNode>(state, "GetDispatchPayload", instanceWithoutSnapshot, signal);

        Assert.True(legacyPayload["legacy"]!.GetValue<bool>());
        Assert.Equal("signal", signalPayload["saleId"]!.GetValue<string>());
    }

    [Fact]
    public void GetDispatchPayloadReturnsNullWhenInstanceAndSignalAreMissing()
    {
        var state = CreatePayloadState();

        var dispatchPayload = Invoke<JsonNode?>(state, "GetDispatchPayload", null!, null!);

        Assert.Null(dispatchPayload);
    }

    [Fact]
    public void GetDispatchPayloadReturnsNullWhenTriggerEnvelopeHasNoPayload()
    {
        var state = CreatePayloadState();
        var instance = Instance(JsonNode.Parse("""{"trigger":{},"stages":{},"variables":{}}"""));

        var dispatchPayload = Invoke<JsonNode?>(state, "GetDispatchPayload", instance, JsonNode.Parse("""{"signal":true}""")!);

        Assert.Null(dispatchPayload);
    }

    [Fact]
    public void ApplyTaskPayloadsStoreRequestAndResponseByStageAndTask()
    {
        var state = CreatePayloadState();
        var instance = Instance(JsonNode.Parse("""{"saleId":"legacy"}"""));
        var request = JsonNode.Parse("""{"sku":"sku-1","quantity":2}""")!;
        var response = JsonNode.Parse("""{"reserved":true}""")!;

        var withRequest = Invoke<JsonNode>(
            state,
            "ApplyTaskRequestPayload",
            instance,
            "inventory",
            "inventories.reserve",
            request);
        instance.SnapshotPayload = withRequest;
        var withResponse = Invoke<JsonNode>(
            state,
            "ApplyCallbackPayload",
            instance,
            "inventory",
            "inventories.reserve",
            response);
        request["sku"] = "changed";
        response["reserved"] = false;

        var task = withResponse["stages"]!["inventory"]!["tasks"]!["inventories.reserve"]!;
        Assert.Equal("legacy", withResponse["trigger"]!["payload"]!["saleId"]!.GetValue<string>());
        Assert.Equal("sku-1", task["request"]!["sku"]!.GetValue<string>());
        Assert.True(task["response"]!["reserved"]!.GetValue<bool>());
    }

    [Fact]
    public void ApplyTaskPayloadsAcceptNullRequestAndResponsePayloads()
    {
        var state = CreatePayloadState();
        var instance = Instance(JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}},"stages":{},"variables":{}}"""));

        var withRequest = Invoke<JsonNode>(
            state,
            "ApplyTaskRequestPayload",
            instance,
            "inventory",
            "inventories.reserve",
            null!);
        instance.SnapshotPayload = withRequest;
        var withResponse = Invoke<JsonNode>(
            state,
            "ApplyCallbackPayload",
            instance,
            "inventory",
            "inventories.reserve",
            null!);

        var task = withResponse["stages"]!["inventory"]!["tasks"]!["inventories.reserve"]!.AsObject();
        Assert.True(task.ContainsKey("request"));
        Assert.True(task.ContainsKey("response"));
    }

    [Fact]
    public void ApplyTaskPayloadCreatesStructuredRootWhenSnapshotIsNull()
    {
        var state = CreatePayloadState();
        var instance = Instance();

        var withRequest = Invoke<JsonNode>(
            state,
            "ApplyTaskRequestPayload",
            instance,
            "inventory",
            "inventories.reserve",
            JsonNode.Parse("""{"sku":"sku-1"}""")!);

        Assert.NotNull(withRequest["trigger"]);
        Assert.Equal("sku-1", withRequest["stages"]!["inventory"]!["tasks"]!["inventories.reserve"]!["request"]!["sku"]!.GetValue<string>());
    }

    [Fact]
    public void ApplyTaskPayloadsAcceptMissingInstance()
    {
        var state = CreatePayloadState();

        var withRequest = Invoke<JsonNode>(
            state,
            "ApplyTaskRequestPayload",
            null!,
            "inventory",
            "inventories.reserve",
            JsonNode.Parse("""{"sku":"sku-1"}""")!);
        var withResponse = Invoke<JsonNode>(
            state,
            "ApplyCallbackPayload",
            null!,
            "inventory",
            "inventories.reserve",
            JsonNode.Parse("""{"reserved":true}""")!);

        Assert.Equal("sku-1", withRequest["stages"]!["inventory"]!["tasks"]!["inventories.reserve"]!["request"]!["sku"]!.GetValue<string>());
        Assert.True(withResponse["stages"]!["inventory"]!["tasks"]!["inventories.reserve"]!["response"]!["reserved"]!.GetValue<bool>());
    }

    [Fact]
    public void PayloadContextFactoryExtractsStructuredTriggerAndProjectsMetadataBranches()
    {
        var instance = Instance(JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}},"stages":{},"variables":{}}"""));
        var propagation = new OrchestrationPropagationMetadata();
        propagation.Items[OrchestrationMetadataConstants.LegacyTriggerMetadataKey] =
            JsonNode.Parse("""{"legacy":true}""")!;
        propagation.Items[OrchestrationMetadataConstants.TriggerMetadataKey] =
            JsonNode.Parse("""{"current":true}""")!;
        propagation.Items["alias-source"] = JsonNode.Parse("""{"value":7}""")!;
        propagation.Items["self-null-source"] = JsonNode.Parse("""{"value":9}""")!;
        propagation.Items["null-item"] = null!;
        instance.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonSerializer.SerializeToNode(propagation)!;
        var factory = new DefaultOrchestrationPayloadContextFactory();

        var context = factory.Create(
            instance,
            "inventory",
            "reserve",
            [
                null!,
                CreateMetadataDescriptor(string.Empty, "alias-source"),
                CreateMetadataDescriptor("missing", "missing-source"),
                CreateMetadataDescriptor("alias-source", string.Empty),
                CreateMetadataDescriptor("self-null-source", null!),
                CreateMetadataDescriptor("alias", "alias-source"),
                CreateMetadataDescriptor("null-alias", "null-item")
            ]);

        Assert.Equal("sale-1", context.TriggerPayload!["saleId"]!.GetValue<string>());
        Assert.True(context.MetadataPayload![OrchestrationMetadataConstants.TriggerMetadataKey]!["current"]!.GetValue<bool>());
        Assert.Null(context.MetadataPayload![OrchestrationMetadataConstants.LegacyTriggerMetadataKey]);
        Assert.Equal(7, context.MetadataPayload!["alias-source"]!["value"]!.GetValue<int>());
        Assert.Equal(9, context.MetadataPayload!["self-null-source"]!["value"]!.GetValue<int>());
        Assert.Equal(7, context.MetadataPayload!["alias"]!["value"]!.GetValue<int>());
        Assert.True(context.MetadataPayload.AsObject().ContainsKey("null-alias"));
    }

    [Fact]
    public void PayloadContextFactoryCoversNullContextAndTryResolveGuardBranches()
    {
        var factory = new DefaultOrchestrationPayloadContextFactory();
        var context = factory.Create(Instance(), "stage", "task");
        var legacyContext = factory.Create(Instance(JsonNode.Parse("""{"legacy":true}""")), "stage", "task");
        var explicitMetadataContext = Instance(JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-2"}}}"""));
        var propagation = new OrchestrationPropagationMetadata();
        propagation.Items["Tenant"] = JsonValue.Create("north")!;
        explicitMetadataContext.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonSerializer.SerializeToNode(propagation)!;
        var descriptorContext = factory.Create(
            explicitMetadataContext,
            "stage",
            "task",
            [CreateMetadataDescriptor("tenant", "tenant")]);
        var legacyOnlyContext = Instance(JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-3"}}}"""));
        var legacyPropagation = new OrchestrationPropagationMetadata();
        legacyPropagation.Items[OrchestrationMetadataConstants.LegacyTriggerMetadataKey] =
            JsonNode.Parse("""{"tenant":"legacy"}""")!;
        legacyOnlyContext.Metadata[OrchestrationMetadataConstants.OrchestrationPropagationMetadataKey] =
            JsonSerializer.SerializeToNode(legacyPropagation)!;
        var legacyMetadataContext = factory.Create(legacyOnlyContext, "stage", "task");

        Assert.Null(context.ContextPayload);
        Assert.Null(context.TriggerPayload);
        Assert.True(legacyContext.TriggerPayload!["legacy"]!.GetValue<bool>());
        Assert.Equal("north", descriptorContext.MetadataPayload!["tenant"]!.GetValue<string>());
        Assert.Equal("legacy", legacyMetadataContext.MetadataPayload![OrchestrationMetadataConstants.TriggerMetadataKey]!["tenant"]!.GetValue<string>());
        Assert.False(InvokeTryResolve(null!, "source", out _));
        Assert.False(InvokeTryResolve(new Dictionary<string, JsonNode>(), string.Empty, out _));
        Assert.Throws<TargetInvocationException>(() => InvokeTryResolve(
            new Dictionary<string, JsonNode>
            {
                ["source"] = JsonValue.Create(1)!,
                ["SOURCE"] = JsonValue.Create(2)!
            },
            "Source",
            out _));
    }

    private static object CreatePayloadState()
    {
        var type = typeof(IOrchestrationTimeoutProcessor).Assembly.GetType(
            "Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads.DefaultOrchestrationPayloadState",
            throwOnError: true)!;
        return Activator.CreateInstance(type, nonPublic: true)!;
    }

    private static OrchestrationInstance Instance(JsonNode? snapshotPayload = null)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = Id.New().ToString(),
            SagaId = Id.New().ToString(),
            ExecutionKey = "sales.sale.created",
            SnapshotPayload = snapshotPayload
        };

    private static MetadataDescriptorArtifact CreateMetadataDescriptor(string key, string sourceKey)
        => new(
            Id.New(),
            key,
            sourceKey,
            key,
            string.Empty,
            "JsonSchema",
            """{"type":"object"}""",
            $"{key}-hash");

    private static bool InvokeTryResolve(
        IReadOnlyDictionary<string, JsonNode> items,
        string sourceKey,
        out JsonNode? value)
    {
        var method = typeof(DefaultOrchestrationPayloadContextFactory).GetMethod(
            "TryResolveMetadataItem",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] arguments = [items, sourceKey, null];
        var result = (bool)method.Invoke(null, arguments)!;
        value = (JsonNode?)arguments[2];
        return result;
    }

    private static T Invoke<T>(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, InstanceFlags)!;
        return (T)method.Invoke(target, args)!;
    }
}
