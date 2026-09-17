using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeMuleRegistrationTests
{
    [Fact]
    public void RuntimeMuleRegistrationRejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule.ServiceCollectionExtensions.AddMule(null!, _ => { }));

        var services = new ServiceCollection();
        var builder = services.AddKrackendOrchestrationsRuntime();

        Assert.Throws<ArgumentNullException>(() => builder.AddMule(null!));
    }

    [Fact]
    public void RuntimeMuleRegistrationConfiguresArtifactLifecycleLanes()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<Krackend.Sagas.Orchestrations.Runtime.Replication.RuntimeReplicaOptions>(options =>
        {
            options.ReplicaId = "replica-a";
        });

        services
            .AddKrackendOrchestrationsRuntime()
            .AddMule(_ => { });

        AssertRegistered<IRuntimeConnectionTokenIssuer>(services);
        AssertRegistered<IRuntimeConnectionTokenValidator>(services);
        AssertRegistered<IControlPlaneAccessTokenProvider>(services);
        AssertRegistered<IRuntimeDesignNodeConnectionService>(services);

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;

        Assert.Equal(MuleRecoveryMode.Polling, settings.RecoveryMode);
        Assert.True(settings.Lanes.ContainsKey(RuntimeArtifactProjectionSchedulerDefaults.Lane));
        Assert.True(settings.Lanes.ContainsKey("runtime-standup:replica-a"));

        var projectionLane = settings.Lanes[RuntimeArtifactProjectionSchedulerDefaults.Lane];
        Assert.Equal(256, projectionLane.WorkerCount);
        Assert.Equal(256, projectionLane.MaxDegreeOfParallelism);
        Assert.Equal(250, projectionLane.DispatchBatchSize);
        Assert.Equal(0, projectionLane.DispatchQueueCapacity);
        Assert.Equal(0, projectionLane.ExecutionQueueCapacity);
        Assert.Equal(int.MaxValue, projectionLane.MaxDrainBatchesPerCycle);
        Assert.Equal(0, projectionLane.MaxDrainActionsPerCycle);
        Assert.True(projectionLane.DrainUntilEmpty);
    }

    [Fact]
    public void RuntimeMuleRegistrationAllowsRuntimeLaneDefaultsToBeConfigured()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<Krackend.Sagas.Orchestrations.Runtime.Replication.RuntimeReplicaOptions>(options =>
        {
            options.ReplicaId = "replica-b";
        });

        services
            .AddKrackendOrchestrationsRuntime()
            .AddMule(
                _ => { },
                options =>
                {
                    options.ArtifactLifecycleWorkerCount = 12;
                    options.ArtifactLifecycleMaxDegreeOfParallelism = 24;
                    options.ArtifactLifecycleDispatchBatchSize = 99;
                    options.ArtifactLifecycleDispatchQueueCapacity = 500;
                    options.ArtifactLifecycleExecutionQueueCapacity = 750;
                    options.ArtifactLifecycleMaxDrainBatchesPerCycle = 3;
                    options.ArtifactLifecycleMaxDrainActionsPerCycle = 44;
                    options.ArtifactLifecycleDrainUntilEmpty = false;
                });

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;
        var standupLane = settings.Lanes["runtime-standup:replica-b"];

        Assert.Equal(12, standupLane.WorkerCount);
        Assert.Equal(24, standupLane.MaxDegreeOfParallelism);
        Assert.Equal(99, standupLane.DispatchBatchSize);
        Assert.Equal(500, standupLane.DispatchQueueCapacity);
        Assert.Equal(750, standupLane.ExecutionQueueCapacity);
        Assert.Equal(3, standupLane.MaxDrainBatchesPerCycle);
        Assert.Equal(44, standupLane.MaxDrainActionsPerCycle);
        Assert.False(standupLane.DrainUntilEmpty);
    }

    private static void AssertRegistered<TService>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
    }
}
