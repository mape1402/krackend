namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class RuntimeIngressConfigurationProjectorTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProjectAsync_WhenMessagingTriggerIsValid_CreatesTriggerAndBackchannelConfigurations()
    {
        var artifact = CreateRuntimeArtifact(CreateArtifact(
            "sales.sale.created",
            new SemanticVersion(1, 2, 0),
            Trigger("events.sales.sale.created", new SemanticVersion(1, 0, 0), isEnabled: true)));
        var (projector, repository) = CreateProjector();
        var serializer = new DefaultMessagingConfigurationSerializer();

        await projector.ProjectAsync(artifact);

        var configurations = repository.Configurations.OrderBy(configuration => configuration.IngressKind).ToArray();
        Assert.Equal(2, configurations.Length);
        var backchannel = Assert.Single(configurations, configuration => configuration.IngressKind == IngressKind.Backchannel);
        var trigger = Assert.Single(configurations, configuration => configuration.IngressKind == IngressKind.Trigger);
        var triggerSettings = serializer.Deserialize(trigger.SettingsPayload);
        var backchannelSettings = serializer.Deserialize(backchannel.SettingsPayload);

        Assert.Equal(IngressTransport.Messaging, trigger.IngressTransport);
        Assert.Equal("events.sales.sale.created", triggerSettings.Topic);
        Assert.Equal("1.0.0", triggerSettings.Version);
        Assert.Equal("orchestrations.sales.sale.created", backchannelSettings.Topic);
        Assert.Equal("1.2.0", backchannelSettings.Version);
    }

    [Fact]
    public async Task ProjectAsync_WhenAllMessagingTriggersAreDisabled_FailsWithoutPersistingConfigurations()
    {
        var artifact = CreateRuntimeArtifact(CreateArtifact(
            "sales.sale.created",
            new SemanticVersion(1, 2, 0),
            Trigger("events.sales.sale.created", new SemanticVersion(1, 0, 0), isEnabled: false)));
        var (projector, repository) = CreateProjector();

        var exception = await Assert.ThrowsAsync<IngressProjectionConfigurationException>(
            async () => await projector.ProjectAsync(artifact));

        Assert.Contains("does not contain any enabled messaging trigger", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repository.Configurations);
    }

    [Fact]
    public async Task ProjectAsync_WhenMessagingTriggerTopicIsMissing_FailsWithoutPersistingConfigurations()
    {
        var artifact = CreateRuntimeArtifact(CreateArtifact(
            "sales.sale.created",
            new SemanticVersion(1, 2, 0),
            Trigger(string.Empty, new SemanticVersion(1, 0, 0), isEnabled: true)));
        var (projector, repository) = CreateProjector();

        var exception = await Assert.ThrowsAsync<IngressProjectionConfigurationException>(
            async () => await projector.ProjectAsync(artifact));

        Assert.Contains("does not define a messaging topic", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repository.Configurations);
    }

    private static (IRuntimeIngressConfigurationProjector Projector, RecordingRuntimeIngressConfigurationRepository Repository) CreateProjector()
    {
        var repository = new RecordingRuntimeIngressConfigurationRepository();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<IRuntimeIngressConfigurationRepository>(repository);
        services.AddKrackendOrchestrationsRuntime();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IRuntimeIngressConfigurationProjector>(), repository);
    }

    private static RuntimeOrchestrationArtifact CreateRuntimeArtifact(OrchestrationArtifact artifact)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = artifact.Key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
            Version = artifact.Version,
            ArtifactChecksum = artifact.Checksum,
            ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions)!,
            Status = RuntimeOrchestrationArtifactStatus.Pending,
            IngressGeneration = 1,
            IsActive = true,
            LoadedToCache = false,
            DeployedOnUtc = DateTime.UtcNow
        };

    private static OrchestrationArtifact CreateArtifact(
        string key,
        SemanticVersion version,
        TriggerBindingArtifact trigger)
        => new(
            Id.New(),
            Id.New(),
            key,
            key,
            "sales",
            version,
            new Checksum("checksum"),
            [trigger],
            [],
            []);

    private static TriggerBindingArtifact Trigger(
        string topic,
        SemanticVersion version,
        bool isEnabled)
        => new(
            Id.New(),
            TriggerType.Event,
            new EventTriggerChannelArtifact(Binding(SchemaContractKind.Event), topic, version),
            isEnabled);

    private static SchemaBindingArtifact Binding(SchemaContractKind kind)
        => new(
            Id.New(),
            ElementType.Orchestration,
            Id.New(),
            Id.New(),
            "sales.sale.created",
            new SemanticVersion(1, 0, 0),
            Id.New(),
            true)
        {
            ContractKind = kind
        };
}
