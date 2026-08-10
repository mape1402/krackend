using Krackend.Sagas.Orchestrations.Messaging.Pigeon;

namespace Krackend.Sagas.Orchestrations.Tests.Messaging;

public sealed class PigeonPackageTests
{
    [Fact]
    public void PigeonAdapterExposesExpectedPublicSurface()
    {
        Assert.Equal(
            "Krackend.Sagas.Orchestrations.Messaging.Pigeon",
            typeof(MessagingPigeonMarker).Namespace);

        Assert.NotNull(typeof(ServiceCollectionExtensions)
            .GetMethod(nameof(ServiceCollectionExtensions.AddKrackendSagasOrchestrationsMessagingPigeon)));
    }
}
