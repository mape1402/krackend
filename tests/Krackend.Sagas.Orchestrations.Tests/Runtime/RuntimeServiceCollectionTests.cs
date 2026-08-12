using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Engine;
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

    [Fact]
    public void EngineRegistersNoopReactivePublisherByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<NoopRuntimeReactiveEventPublisher>(
            provider.GetRequiredService<IRuntimeReactiveEventPublisher>());
    }

    [Fact]
    public void EngineRegistersMessagingRuntimeTaskDispatcherByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<IRuntimeTaskDispatcherResolver>();
        var dispatcher = resolver.Resolve("Messaging");

        Assert.IsType<MessagingRuntimeTaskDispatcher>(dispatcher);
    }

    [Fact]
    public void EngineRegistersRuntimeConditionEvaluatorByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RuntimeConditionEvaluator>(
            provider.GetRequiredService<IRuntimeConditionEvaluator>());
    }

    [Fact]
    public void EngineRegistersRuntimePayloadTransformerByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RuntimePayloadTransformer>(
            provider.GetRequiredService<IRuntimePayloadTransformer>());
    }

    [Fact]
    public void EngineRegistersRuntimeRetryPolicyEvaluatorByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RuntimeRetryPolicyEvaluator>(
            provider.GetRequiredService<IRuntimeRetryPolicyEvaluator>());
    }

    [Fact]
    public void EngineRegistersRuntimeErrorPolicyResolverByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RuntimeErrorPolicyResolver>(
            provider.GetRequiredService<IRuntimeErrorPolicyResolver>());
    }

    [Fact]
    public void EngineRegistersRuntimeCompensationPlanBuilderByDefault()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsEngine();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RuntimeCompensationPlanBuilder>(
            provider.GetRequiredService<IRuntimeCompensationPlanBuilder>());
    }
}
