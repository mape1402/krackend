using Krackend.Sagas.Orchestrations.Client.Abstractions;

namespace Krackend.Sagas.Orchestrations.Tests.Client;

public sealed class ClientAbstractionsTests
{
    [Fact]
    public void SemanticVersionDefaultIsOneZeroZero()
    {
        Assert.Equal("1.0.0", OrchestrationSemanticVersion.Default.ToString());
    }

    [Theory]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("2.15.30", 2, 15, 30)]
    public void SemanticVersionParsesValidVersion(string value, int major, int minor, int patch)
    {
        var version = OrchestrationSemanticVersion.Parse(value);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(value, version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("1.0.x")]
    [InlineData("1.-1.0")]
    [InlineData("v1.0.0")]
    public void SemanticVersionRejectsInvalidVersion(string value)
    {
        Assert.False(OrchestrationSemanticVersion.TryParse(value, out _));
        Assert.Throws<FormatException>(() => OrchestrationSemanticVersion.Parse(value));
    }

    [Fact]
    public void OutputDescriptorRequiresTopic()
    {
        Assert.Throws<ArgumentException>(() => new OrchestrationOutputDescriptor(" "));
    }

    [Fact]
    public void OutputDescriptorUsesDefaultVersionWhenVersionIsNotProvided()
    {
        var descriptor = new OrchestrationOutputDescriptor("orders.completed");

        Assert.Equal("orders.completed", descriptor.Topic);
        Assert.Equal(OrchestrationSemanticVersion.Default, descriptor.Version);
    }

    [Fact]
    public void ExecutionContextCanPublishWhenMetadataOrManualOutputExists()
    {
        var standalone = new OrchestrationExecutionContext
        {
            Mode = OrchestrationClientExecutionMode.Standalone,
            Output = new OrchestrationOutputDescriptor("orders.completed")
        };

        var empty = new OrchestrationExecutionContext
        {
            Mode = OrchestrationClientExecutionMode.Standalone
        };

        Assert.True(standalone.CanPublish);
        Assert.False(empty.CanPublish);
    }

    [Fact]
    public void ClientAbstractionsExposeExpectedPublicSurface()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Client.Abstractions",
            typeof(ClientAbstractionsMarker).Namespace);
    }
}
