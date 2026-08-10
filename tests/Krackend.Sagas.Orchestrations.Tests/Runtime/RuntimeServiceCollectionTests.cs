using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Intake.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeServiceCollectionTests
{
    [Fact]
    public void RuntimeAndMessagingServicesResolve()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsRuntime(options =>
        {
            options.EnvironmentKey = "local";
        });
        services.AddKrackendSagasOrchestrationsInMemoryIntakeBuffer();
        services.AddKrackendSagasOrchestrationsMessaging();

        using var provider = services.BuildServiceProvider();

        var runtime = provider.GetRequiredService<RuntimeEnvironmentDescriptor>();
        var intake = provider.GetRequiredService<ITriggerIntakeBuffer>();
        var metadataAccessor = provider.GetRequiredService<IOrchestratorMetadataAccessor>();
        var metadataWriter = provider.GetRequiredService<IOrchestratorMetadataWriter>();

        Assert.Equal("local", runtime.EnvironmentKey);
        Assert.NotNull(intake);
        Assert.Same(metadataAccessor, metadataWriter);
    }
}
