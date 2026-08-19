using System.Text.Json;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Web;

namespace Krackend.Sagas.Orchestrations.Tests.Web;

public sealed class RuntimeArtifactIngressBindingBuilderTests
{
    [Fact]
    public void Build_DerivesMessagingTriggerAndBackChannelFromArtifact()
    {
        var artifact = CreateRuntimeArtifact(CreateArtifact(
            key: "sales.sale.created",
            version: new SemanticVersion(2, 1, 0),
            new TriggerBindingArtifact(
                Id.New(),
                TriggerType.Event,
                new EventTriggerChannelArtifact(
                    CreateSchema(),
                    "events.sale.created",
                    new SemanticVersion(1, 0, 3)),
                true)));

        var bindings = new RuntimeArtifactIngressBindingBuilder().Build(artifact);

        var trigger = Assert.Single(bindings.MessagingTriggers);
        Assert.Equal("sales.sale.created", bindings.OrchestrationKey);
        Assert.Equal("2.1.0", bindings.OrchestrationVersion);
        Assert.Equal(RuntimeIngressBindingKind.Trigger, trigger.Kind);
        Assert.Equal("events.sale.created", trigger.Topic);
        Assert.Equal("1.0.3", trigger.Version);
        Assert.Equal(RuntimeIngressBindingKind.BackChannel, bindings.BackChannel.Kind);
        Assert.Equal("orchestrations.sales.sale.created", bindings.BackChannel.Topic);
        Assert.Equal("2.1.0", bindings.BackChannel.Version);
        Assert.Empty(bindings.Skipped);
    }

    [Fact]
    public void Build_SkipsDisabledTriggers()
    {
        var artifact = CreateRuntimeArtifact(CreateArtifact(
            key: "billing.invoice.created",
            version: new SemanticVersion(1, 0, 0),
            new TriggerBindingArtifact(
                Id.New(),
                TriggerType.Event,
                new EventTriggerChannelArtifact(
                    CreateSchema(),
                    "events.invoice.created",
                    new SemanticVersion(1, 0, 0)),
                false)));

        var bindings = new RuntimeArtifactIngressBindingBuilder().Build(artifact);

        Assert.Empty(bindings.MessagingTriggers);
        var skipped = Assert.Single(bindings.Skipped);
        Assert.Equal("billing.invoice.created", skipped.OrchestrationKey);
        Assert.Contains("disabled", skipped.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_SkipsMessagingTriggersWithoutTopic()
    {
        var artifact = CreateRuntimeArtifact(CreateArtifact(
            key: "inventory.reserve",
            version: new SemanticVersion(1, 2, 0),
            new TriggerBindingArtifact(
                Id.New(),
                TriggerType.Event,
                new EventTriggerChannelArtifact(
                    CreateSchema(),
                    "",
                    new SemanticVersion(1, 0, 0)),
                true)));

        var bindings = new RuntimeArtifactIngressBindingBuilder().Build(artifact);

        Assert.Empty(bindings.MessagingTriggers);
        var skipped = Assert.Single(bindings.Skipped);
        Assert.Contains("topic", skipped.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("orchestrations.inventory.reserve", bindings.BackChannel.Topic);
    }

    private static OrchestrationArtifact CreateArtifact(
        string key,
        SemanticVersion version,
        params TriggerBindingArtifact[] triggers)
    {
        return new OrchestrationArtifact(
            Id.New(),
            Id.New(),
            key,
            key,
            "demo",
            version,
            new Checksum("checksum"),
            triggers,
            Array.Empty<VariableDefinitionArtifact>(),
            Array.Empty<StageArtifact>());
    }

    private static RuntimeOrchestrationArtifact CreateRuntimeArtifact(OrchestrationArtifact artifact)
    {
        return new RuntimeOrchestrationArtifact
        {
            Id = Id.New(),
            EnvironmentKey = "",
            OrchestrationDefinitionKey = artifact.Key,
            ArtifactType = "orchestration.deploy",
            SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
            Version = artifact.Version,
            ArtifactChecksum = artifact.Checksum,
            ArtifactPayload = JsonSerializer.SerializeToNode(artifact)!,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        };
    }

    private static SchemaBindingArtifact CreateSchema()
        => new(
            Id.New(),
            ElementType.Orchestration,
            Id.New(),
            Id.New(),
            "demo.contract",
            new SemanticVersion(1, 0, 0),
            Id.New(),
            false);
}
