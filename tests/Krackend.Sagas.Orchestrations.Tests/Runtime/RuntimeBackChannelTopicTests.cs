using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeBackChannelTopicTests
{
    [Fact]
    public void Build_AppendsSemanticVersionToBackChannelTopic()
    {
        var topic = RuntimeBackChannelTopic.Build("order.fulfillment", "2.1.3");

        Assert.Equal("orchestrations.order.fulfillment.v2-1-3", topic);
    }

    [Fact]
    public void Build_DoesNotAppendVersionTwice()
    {
        var topic = RuntimeBackChannelTopic.Build("orchestrations.order.fulfillment.v2-1-3", "2.1.3");

        Assert.Equal("orchestrations.order.fulfillment.v2-1-3", topic);
    }
}
