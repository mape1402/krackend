namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class MessagingIngressConfigurationSelectorTests
{
    [Fact]
    public void SelectMatchingReturnsEmptyForNullEmptyAndNonMatchingConfigurations()
    {
        var selector = CreateSelector();

        Assert.Empty(selector.SelectMatching(null!, "orders.created", "1.0.0"));
        Assert.Empty(selector.SelectMatching([], "orders.created", "1.0.0"));
        Assert.Empty(selector.SelectMatching(
            [CreateTrigger("artifact-v1", "orders.created", "1.0.0", "orders.fulfillment", "1.0.0", DateTime.UtcNow)],
            "orders.cancelled",
            "1.0.0"));
    }

    [Fact]
    public void SerializerRejectsNullAndEmptyPayloads()
    {
        var serializer = new DefaultMessagingConfigurationSerializer();

        Assert.Throws<ArgumentNullException>(() => serializer.Serialize(null!));
        Assert.Throws<ArgumentException>(() => serializer.Deserialize(" "));
    }

    [Fact]
    public void SelectMatchingKeepsFanoutAcrossDifferentOrchestrations()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-a", "orders.created", "1.0.0", "orders.fulfillment", "1.0.0", DateTime.UtcNow.AddMinutes(-2)),
            CreateTrigger("artifact-b", "orders.created", "1.0.0", "orders.analytics", "1.0.0", DateTime.UtcNow.AddMinutes(-1))
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "1.0.0");

        Assert.Equal(["artifact-a", "artifact-b"], selected.Select(x => x.ArtifactId).OrderBy(x => x));
    }

    [Fact]
    public void SelectMatchingPrefersArtifactVersionThatMatchesIncomingMessageVersion()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-v1", "orders.created", "1.0.0", "orders.fulfillment", "1.0.0", DateTime.UtcNow.AddMinutes(-5)),
            CreateTrigger("artifact-v2", "orders.created", "1.0.0", "orders.fulfillment", "2.0.0", DateTime.UtcNow.AddMinutes(-1))
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "1.0.0");

        Assert.Equal("artifact-v1", Assert.Single(selected).ArtifactId);
    }

    [Fact]
    public void SelectMatchingUsesNewestArtifactWhenNoArtifactVersionMatchesIncomingMessageVersion()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-v2", "orders.created", "1.0.0", "orders.fulfillment", "2.0.0", DateTime.UtcNow.AddMinutes(-5)),
            CreateTrigger("artifact-v3", "orders.created", "1.0.0", "orders.fulfillment", "3.0.0", DateTime.UtcNow.AddMinutes(-1))
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "1.0.0");

        Assert.Equal("artifact-v3", Assert.Single(selected).ArtifactId);
    }

    [Fact]
    public void SelectMatchingFallsBackToDeployDateWhenArtifactVersionIsInvalid()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-old", "orders.created", "1.0.0", "orders.fulfillment", "bad", DateTime.UtcNow.AddMinutes(-5)),
            CreateTrigger("artifact-new", "orders.created", "1.0.0", "orders.fulfillment", "also-bad", DateTime.UtcNow.AddMinutes(-1))
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "1.0.0");

        Assert.Equal("artifact-new", Assert.Single(selected).ArtifactId);
    }

    [Fact]
    public void SelectMatchingIgnoresNonMatchingMessageVersions()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-v1", "orders.created", "1.0.0", "orders.fulfillment", "1.0.0", DateTime.UtcNow),
            CreateTrigger("artifact-v2", "orders.created", "2.0.0", "orders.fulfillment", "2.0.0", DateTime.UtcNow)
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "2.0.0");

        Assert.Equal("artifact-v2", Assert.Single(selected).ArtifactId);
    }

    [Fact]
    public void SelectMatchingIgnoresMalformedMessagingSettings()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-valid", "orders.created", "1.0.0", "orders.fulfillment", "1.0.0", DateTime.UtcNow),
            new IngressConfiguration
            {
                Id = "artifact-bad:trigger",
                ArtifactId = "artifact-bad",
                OrchestrationDefinitionKey = "orders.fulfillment",
                OrchestrationVersion = "1.0.0",
                DeployedOnUtc = DateTime.UtcNow.AddMinutes(1),
                IngressKind = IngressKind.Trigger,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = "{"
            }
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "1.0.0");

        Assert.Equal("artifact-valid", Assert.Single(selected).ArtifactId);
    }

    [Fact]
    public void SelectMatchingIgnoresConfigurationsForOtherTransports()
    {
        var selector = CreateSelector();
        var configurations = new[]
        {
            CreateTrigger("artifact-valid", "orders.created", "1.0.0", "orders.fulfillment", "1.0.0", DateTime.UtcNow),
            new IngressConfiguration
            {
                Id = "artifact-http:trigger",
                ArtifactId = "artifact-http",
                OrchestrationDefinitionKey = "orders.fulfillment",
                OrchestrationVersion = "1.0.0",
                DeployedOnUtc = DateTime.UtcNow.AddMinutes(1),
                IngressKind = IngressKind.Trigger,
                IngressTransport = IngressTransport.Http,
                SettingsPayload = """{"topic":"orders.created","version":"1.0.0"}"""
            }
        };

        var selected = selector.SelectMatching(configurations, "orders.created", "1.0.0");

        Assert.Equal("artifact-valid", Assert.Single(selected).ArtifactId);
    }

    private static IMessagingIngressConfigurationSelector CreateSelector()
        => new DefaultMessagingIngressConfigurationSelector(
            new DefaultMessagingConfigurationSerializer(),
            NullLogger<DefaultMessagingIngressConfigurationSelector>.Instance);

    private static IngressConfiguration CreateTrigger(
        string artifactId,
        string topic,
        string messageVersion,
        string orchestrationDefinitionKey,
        string orchestrationVersion,
        DateTime deployedOnUtc)
    {
        var serializer = new DefaultMessagingConfigurationSerializer();
        return new IngressConfiguration
        {
            Id = $"{artifactId}:trigger",
            ArtifactId = artifactId,
            OrchestrationDefinitionKey = orchestrationDefinitionKey,
            OrchestrationVersion = orchestrationVersion,
            DeployedOnUtc = deployedOnUtc,
            IngressKind = IngressKind.Trigger,
            IngressTransport = IngressTransport.Messaging,
            SettingsPayload = serializer.Serialize(new MessagingConfiguration
            {
                Topic = topic,
                Version = messageVersion
            })
        };
    }
}
