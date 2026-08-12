using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimePayloadTransformerTests
{
    [Fact]
    public void Transform_WhenTransformationIsEmpty_ReturnsPayloadClone()
    {
        var transformer = new RuntimePayloadTransformer();
        var source = JsonNode.Parse("""{"orderId":"order-1"}""")!;

        var result = transformer.Transform(new JsonObject(), source);

        Assert.False(result.WasTransformed);
        Assert.Equal("""{"orderId":"order-1"}""", result.Payload.ToJsonString());
        Assert.NotSame(source, result.Payload);
    }

    [Fact]
    public void Transform_WhenDslTransformationIsConfigured_ReturnsIdentityPayloadForNow()
    {
        var transformer = new RuntimePayloadTransformer();
        var transformation = JsonNode.Parse("""{"Engine":0,"Configuration":{"$artifactType":"dsl"}}""")!.AsObject();

        var result = transformer.Transform(transformation, JsonNode.Parse("""{"orderId":"order-1"}"""));

        Assert.True(result.WasTransformed);
        Assert.Equal("DSL", result.Engine);
        Assert.Equal("""{"orderId":"order-1"}""", result.Payload.ToJsonString());
    }

    [Fact]
    public void Transform_WhenEngineIsUnsupported_FailsExplicitly()
    {
        var transformer = new RuntimePayloadTransformer();
        var transformation = JsonNode.Parse("""{"Engine":"Scripting"}""")!.AsObject();

        var ex = Assert.Throws<NotSupportedException>(() => transformer.Transform(transformation, JsonNode.Parse("""{"orderId":"order-1"}""")));

        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
