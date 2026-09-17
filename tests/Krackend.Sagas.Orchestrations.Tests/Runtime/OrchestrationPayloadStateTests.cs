namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
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

    private static T Invoke<T>(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, InstanceFlags)!;
        return (T)method.Invoke(target, args)!;
    }
}
