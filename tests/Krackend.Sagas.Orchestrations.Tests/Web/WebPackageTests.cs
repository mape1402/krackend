using Krackend.Sagas.Orchestrations.Web;

namespace Krackend.Sagas.Orchestrations.Tests.Web;

public sealed class WebPackageTests
{
    [Fact]
    public void WebPackageExposesHostIntegrationSurface()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Web",
            typeof(OrchestrationsWebMarker).Namespace);

        Assert.NotNull(typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(ServiceCollectionExtensions.AddKrackendSagasOrchestrationsWeb)));

        Assert.NotNull(typeof(RuntimeArtifactEndpointRouteBuilderExtensions)
            .GetMethod(nameof(RuntimeArtifactEndpointRouteBuilderExtensions.MapKrackendSagasOrchestrationsArtifactEndpoints)));

        Assert.NotNull(typeof(RuntimeEngineEndpointRouteBuilderExtensions)
            .GetMethod(nameof(RuntimeEngineEndpointRouteBuilderExtensions.MapKrackendSagasOrchestrationsEngineEndpoints)));
    }
}
