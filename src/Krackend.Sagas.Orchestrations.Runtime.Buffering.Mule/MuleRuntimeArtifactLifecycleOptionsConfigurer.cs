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
    private readonly KrackendOrchestrationsMuleRuntimeOptions _runtimeOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MuleRuntimeArtifactLifecycleOptionsConfigurer"/> class.
    /// </summary>
    public MuleRuntimeArtifactLifecycleOptionsConfigurer(
        IRuntimeReplicaIdentity replicaIdentity,
        IOptions<KrackendOrchestrationsMuleRuntimeOptions> runtimeOptions)
    {
        _replicaIdentity = replicaIdentity ?? throw new ArgumentNullException(nameof(replicaIdentity));
        _runtimeOptions = runtimeOptions?.Value ?? throw new ArgumentNullException(nameof(runtimeOptions));
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

        EnsureLane(options, RuntimeArtifactProjectionSchedulerDefaults.Lane, priority: 500, weight: 10, _runtimeOptions);
        EnsureLane(options, _replicaIdentity.LocalStandupLane, priority: 450, weight: 10, _runtimeOptions);
    }

    private static void EnsureLane(
        MuleSettings settings,
        string lane,
        int priority,
        int weight,
        KrackendOrchestrationsMuleRuntimeOptions runtimeOptions)
    {
        if (settings.Lanes.ContainsKey(lane))
        {
            return;
        }

        settings.Lanes[lane] = new MuleLaneSettings
        {
            Priority = priority,
            Weight = weight,
            WorkerCount = Math.Max(1, runtimeOptions.ArtifactLifecycleWorkerCount),
            MaxDegreeOfParallelism = Math.Max(1, runtimeOptions.ArtifactLifecycleMaxDegreeOfParallelism),
            DispatchBatchSize = Math.Max(1, runtimeOptions.ArtifactLifecycleDispatchBatchSize),
            DispatchQueueCapacity = Math.Max(0, runtimeOptions.ArtifactLifecycleDispatchQueueCapacity),
            ExecutionQueueCapacity = Math.Max(0, runtimeOptions.ArtifactLifecycleExecutionQueueCapacity),
            MaxDrainBatchesPerCycle = runtimeOptions.ArtifactLifecycleMaxDrainBatchesPerCycle <= 0
                ? int.MaxValue
                : runtimeOptions.ArtifactLifecycleMaxDrainBatchesPerCycle,
            MaxDrainActionsPerCycle = Math.Max(0, runtimeOptions.ArtifactLifecycleMaxDrainActionsPerCycle),
            DrainUntilEmpty = runtimeOptions.ArtifactLifecycleDrainUntilEmpty
        };
    }
}
