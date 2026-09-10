namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public sealed class IngressRegistryLifecycleTests
{
    [Fact]
    public async Task ShutDownOneDisconnectsConnectorsAndAllowsSameGenerationStandupAgain()
    {
        var connector = new RecordingIngressConnector();
        var registry = CreateRegistry(connector);

        await registry.StandUpOneAsync("artifact-1", 1);
        await registry.ShutDownOneAsync("artifact-1");
        await registry.StandUpOneAsync("artifact-1", 1);

        Assert.Equal(["trigger-ingress", "backchannel-ingress", "trigger-ingress", "backchannel-ingress"], connector.ConnectedConnectorIds);
        Assert.Equal(2, connector.DisconnectedConnectorIds.Count);
        Assert.Contains("trigger-ingress", connector.DisconnectedConnectorIds);
        Assert.Contains("backchannel-ingress", connector.DisconnectedConnectorIds);
    }

    private static IIngressRegistry CreateRegistry(RecordingIngressConnector connector)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddKrackendOrchestrationsRuntime();
        services.RemoveAll<IGetIngressConfigurationByArtifactAccessor>();
        services.RemoveAllKeyed<IIngressConector>(IngressTransport.Messaging);
        services.AddSingleton<IGetIngressConfigurationByArtifactAccessor>(
            new StaticIngressConfigurationByArtifactAccessor(CreateConfigurations()));
        services.AddKeyedSingleton<IIngressConector>(IngressTransport.Messaging, (_, _) => connector);

        return services.BuildServiceProvider().GetRequiredService<IIngressRegistry>();
    }

    private static IngressConfiguration[] CreateConfigurations()
        =>
        [
            new()
            {
                Id = "trigger-ingress",
                ArtifactId = "artifact-1",
                IngressKind = IngressKind.Trigger,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = "{}"
            },
            new()
            {
                Id = "backchannel-ingress",
                ArtifactId = "artifact-1",
                IngressKind = IngressKind.Backchannel,
                IngressTransport = IngressTransport.Messaging,
                SettingsPayload = "{}"
            }
        ];
}
