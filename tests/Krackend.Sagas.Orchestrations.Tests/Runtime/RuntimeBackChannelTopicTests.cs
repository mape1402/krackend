using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeBackChannelTopicTests
{
    [Fact]
    public void Build_UsesOrchestrationsPrefixWithoutAppendingVersion()
    {
        var topic = RuntimeBackChannelTopic.Build("order.fulfillment");

        Assert.Equal("orchestrations.order.fulfillment", topic);
    }

    [Fact]
    public void Build_DoesNotAppendPrefixTwice()
    {
        var topic = RuntimeBackChannelTopic.Build("orchestrations.order.fulfillment");

        Assert.Equal("orchestrations.order.fulfillment", topic);
    }
}
