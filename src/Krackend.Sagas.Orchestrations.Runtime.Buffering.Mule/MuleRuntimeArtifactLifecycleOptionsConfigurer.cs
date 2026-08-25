using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Replication;
using Microsoft.Extensions.Options;
using Mule;

namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Configures Mule lanes required by runtime artifact lifecycle actions.
/// </summary>
internal sealed class MuleRuntimeArtifactLifecycleOptionsConfigurer : IConfigureOptions<MuleSettings>
{
    private readonly IRuntimeReplicaIdentity _replicaIdentity;
    private readonly int _parallelism;

    /// <summary>
    /// Initializes a new instance of the <see cref="MuleRuntimeArtifactLifecycleOptionsConfigurer"/> class.
    /// </summary>
    public MuleRuntimeArtifactLifecycleOptionsConfigurer(IRuntimeReplicaIdentity replicaIdentity)
    {
        _replicaIdentity = replicaIdentity ?? throw new ArgumentNullException(nameof(replicaIdentity));
        _parallelism = Math.Max(64, Environment.ProcessorCount * 20);
    }

    /// <inheritdoc />
    public void Configure(MuleSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RecoveryMode = MuleRecoveryMode.Polling;
        if (options.DispatchInterval <= TimeSpan.Zero || options.DispatchInterval > TimeSpan.FromSeconds(5))
        {
            options.DispatchInterval = TimeSpan.FromSeconds(1);
        }

        EnsureLane(options, RuntimeArtifactProjectionSchedulerDefaults.Lane, priority: 500, weight: 10, parallelism: _parallelism);
        EnsureLane(options, _replicaIdentity.LocalStandupLane, priority: 450, weight: 10, parallelism: _parallelism);
    }

    private static void EnsureLane(
        MuleSettings settings,
        string lane,
        int priority,
        int weight,
        int parallelism)
    {
        if (settings.Lanes.ContainsKey(lane))
        {
            return;
        }

        settings.Lanes[lane] = new MuleLaneSettings
        {
            Priority = priority,
            Weight = weight,
            WorkerCount = parallelism,
            MaxDegreeOfParallelism = parallelism,
            DispatchBatchSize = 250,
            DispatchQueueCapacity = 0,
            ExecutionQueueCapacity = 0,
            MaxDrainBatchesPerCycle = int.MaxValue,
            MaxDrainActionsPerCycle = 0,
            DrainUntilEmpty = true
        };
    }
}
