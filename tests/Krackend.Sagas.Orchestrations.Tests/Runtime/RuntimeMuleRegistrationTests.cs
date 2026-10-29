using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimeMuleRegistrationTests
{
    private const string TriggerLane = "runtime-trigger";
    private const string BackchannelLane = "runtime-backchannel";
    private const string DispatchLane = "runtime-dispatch";
    private const string TimeoutLane = "runtime-timeout";
    private const string StatisticsLane = "runtime-statistics";

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
            .AddMule(
                _ => { },
                options =>
                {
                    options.TotalWorkerCount = 17;
                    options.TotalMaxDegreeOfParallelism = 173;
                });

        AssertRegistered<IRuntimeConnectionTokenIssuer>(services);
        AssertRegistered<IRuntimeConnectionTokenValidator>(services);
        AssertRegistered<IControlPlaneAccessTokenProvider>(services);
        AssertRegistered<IRuntimeDesignNodeConnectionService>(services);

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;

        Assert.Equal(MuleRecoveryMode.Polling, settings.RecoveryMode);
        Assert.True(settings.Lanes.ContainsKey(RuntimeArtifactProjectionSchedulerDefaults.Lane));
        Assert.True(settings.Lanes.ContainsKey("runtime-standup:replica-a"));
        Assert.True(settings.Lanes.ContainsKey(TriggerLane));
        Assert.True(settings.Lanes.ContainsKey(BackchannelLane));
        Assert.True(settings.Lanes.ContainsKey(DispatchLane));
        Assert.True(settings.Lanes.ContainsKey(TimeoutLane));
        Assert.True(settings.Lanes.ContainsKey(StatisticsLane));

        var projectionLane = settings.Lanes[RuntimeArtifactProjectionSchedulerDefaults.Lane];
        Assert.Equal(1, projectionLane.WorkerCount);
        Assert.Equal(4, projectionLane.MaxDegreeOfParallelism);
        Assert.Equal(16, projectionLane.DispatchBatchSize);
        Assert.True(projectionLane.DispatchQueueCapacity > 0);
        Assert.True(projectionLane.ExecutionQueueCapacity > 0);
        Assert.Equal(4, projectionLane.MaxDrainBatchesPerCycle);
        Assert.False(projectionLane.DrainUntilEmpty);
        Assert.True(settings.Lanes[BackchannelLane].WorkerCount >= settings.Lanes[TriggerLane].WorkerCount);
        Assert.True(settings.Lanes[DispatchLane].WorkerCount >= settings.Lanes[TriggerLane].WorkerCount);
        Assert.Equal(1, settings.Lanes[StatisticsLane].WorkerCount);
        Assert.Equal(1, settings.Lanes[StatisticsLane].MaxDegreeOfParallelism);
        Assert.False(settings.Lanes[StatisticsLane].DrainUntilEmpty);
        Assert.Equal(17, settings.Lanes.Values.Sum(lane => lane.WorkerCount));
        Assert.Equal(173, settings.Lanes.Values.Sum(lane => lane.MaxDegreeOfParallelism));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ITaskDispatchCancellationService) &&
            descriptor.ImplementationFactory is not null);
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
                    options.Backchannel.WorkerCount = 17;
                    options.Backchannel.MaxDegreeOfParallelism = 31;
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
        Assert.Equal(17, settings.Lanes[BackchannelLane].WorkerCount);
        Assert.Equal(31, settings.Lanes[BackchannelLane].MaxDegreeOfParallelism);
    }

    [Fact]
    public void RuntimeMuleArtifactLifecycleConfigurerNormalizesBoundaryValues()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<Krackend.Sagas.Orchestrations.Runtime.Replication.RuntimeReplicaOptions>(options =>
        {
            options.ReplicaId = "replica-c";
        });

        services
            .AddKrackendOrchestrationsRuntime()
            .AddMule(
                _ => { },
                options =>
                {
                    options.ArtifactLifecycleWorkerCount = 0;
                    options.ArtifactLifecycleMaxDegreeOfParallelism = -1;
                    options.ArtifactLifecycleDispatchBatchSize = 0;
                    options.ArtifactLifecycleDispatchQueueCapacity = -1;
                    options.ArtifactLifecycleExecutionQueueCapacity = -1;
                    options.ArtifactLifecycleMaxDrainBatchesPerCycle = 0;
                    options.ArtifactLifecycleMaxDrainActionsPerCycle = -1;
                    options.ArtifactLifecycleMaxAttempts = 0;
                    options.ArtifactLifecycleRetryDelay = TimeSpan.Zero;
                    options.Trigger.WorkerCount = 0;
                    options.Trigger.MaxDegreeOfParallelism = -1;
                    options.Trigger.DispatchBatchSize = 0;
                    options.Trigger.DispatchQueueCapacity = -1;
                    options.Trigger.ExecutionQueueCapacity = -1;
                    options.Trigger.MaxDrainBatchesPerCycle = 0;
                    options.Trigger.MaxDrainActionsPerCycle = -1;
                });

        using var provider = services.BuildServiceProvider();
        var configurers = provider.GetServices<IConfigureOptions<MuleSettings>>().ToArray();
        var settings = new MuleSettings
        {
            DispatchInterval = TimeSpan.FromSeconds(10)
        };

        foreach (var configurer in configurers)
        {
            configurer.Configure(settings);
            configurer.Configure(settings);
        }

        var projectionLane = settings.Lanes[RuntimeArtifactProjectionSchedulerDefaults.Lane];
        Assert.Equal(TimeSpan.FromSeconds(1), settings.DispatchInterval);
        Assert.Equal(MuleRecoveryMode.Polling, settings.RecoveryMode);
        Assert.Equal(1, projectionLane.WorkerCount);
        Assert.Equal(1, projectionLane.MaxDegreeOfParallelism);
        Assert.Equal(1, projectionLane.DispatchBatchSize);
        Assert.Equal(0, projectionLane.DispatchQueueCapacity);
        Assert.Equal(0, projectionLane.ExecutionQueueCapacity);
        Assert.Equal(1, projectionLane.MaxDrainBatchesPerCycle);
        Assert.Equal(0, projectionLane.MaxDrainActionsPerCycle);
        Assert.Equal(1, projectionLane.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(1), projectionLane.RetryDelay);
        var triggerLane = settings.Lanes[TriggerLane];
        Assert.Equal(1, triggerLane.WorkerCount);
        Assert.Equal(1, triggerLane.MaxDegreeOfParallelism);
        Assert.Equal(1, triggerLane.DispatchBatchSize);
        Assert.Equal(0, triggerLane.DispatchQueueCapacity);
        Assert.Equal(0, triggerLane.ExecutionQueueCapacity);
        Assert.Equal(1, triggerLane.MaxDrainBatchesPerCycle);
        Assert.Equal(0, triggerLane.MaxDrainActionsPerCycle);

        var zeroIntervalSettings = new MuleSettings
        {
            DispatchInterval = TimeSpan.Zero
        };
        foreach (var configurer in configurers)
        {
            configurer.Configure(zeroIntervalSettings);
        }

        Assert.Equal(TimeSpan.FromSeconds(1), zeroIntervalSettings.DispatchInterval);
    }

    [Fact]
    public void RuntimeMuleRegistrationHighThroughputProfileAllocatesTheExactConfiguredBudget()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<Krackend.Sagas.Orchestrations.Runtime.Replication.RuntimeReplicaOptions>(options =>
        {
            options.ReplicaId = "replica-throughput";
        });

        services.AddKrackendOrchestrationsRuntime().AddMule(
            _ => { },
            options =>
            {
                options.Profile = KrackendMuleRuntimeProfile.HighThroughput;
                options.TotalWorkerCount = 31;
                options.TotalMaxDegreeOfParallelism = 257;
            });

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;

        Assert.Equal(31, settings.Lanes.Values.Sum(lane => lane.WorkerCount));
        Assert.Equal(257, settings.Lanes.Values.Sum(lane => lane.MaxDegreeOfParallelism));
        Assert.True(settings.Lanes[BackchannelLane].MaxDegreeOfParallelism > settings.Lanes[TimeoutLane].MaxDegreeOfParallelism);
        Assert.True(settings.Lanes[DispatchLane].MaxDegreeOfParallelism > settings.Lanes[TriggerLane].MaxDegreeOfParallelism);
    }

    [Fact]
    public void RuntimeMuleRegistrationCustomProfileHonorsTheHostCapacityBudget()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<Krackend.Sagas.Orchestrations.Runtime.Replication.RuntimeReplicaOptions>(options =>
        {
            options.ReplicaId = "replica-custom";
        });

        services.AddKrackendOrchestrationsRuntime().AddMule(
            _ => { },
            options =>
            {
                options.Profile = KrackendMuleRuntimeProfile.Custom;
                options.TotalWorkerCount = 19;
                options.TotalMaxDegreeOfParallelism = 97;
            });

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;

        Assert.Equal(19, settings.Lanes.Values.Sum(lane => lane.WorkerCount));
        Assert.Equal(97, settings.Lanes.Values.Sum(lane => lane.MaxDegreeOfParallelism));
        Assert.Equal(1, settings.Lanes[StatisticsLane].WorkerCount);
        Assert.Equal(4, settings.Lanes[RuntimeArtifactProjectionSchedulerDefaults.Lane].MaxDegreeOfParallelism);
    }

    private static void AssertRegistered<TService>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
    }
}
