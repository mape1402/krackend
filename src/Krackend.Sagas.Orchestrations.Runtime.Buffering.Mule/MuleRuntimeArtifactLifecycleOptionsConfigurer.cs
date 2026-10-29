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

        // Artifact projection is written as part of the runtime deployment transaction. Mule's polling recovery
        // is deliberately retained here as the durable post-commit handoff: it also recovers a process that exits
        // after the deployment transaction commits but before its local notification can be observed.
        //
        // This is not used as a throughput limiter. Projection and standup have isolated lanes with their own
        // capacity budget, so command dispatch, replies and response deadlines remain independently schedulable.
        options.RecoveryMode = MuleRecoveryMode.Polling;
        if (options.DispatchInterval <= TimeSpan.Zero || options.DispatchInterval > TimeSpan.FromSeconds(5))
        {
            options.DispatchInterval = TimeSpan.FromSeconds(1);
        }

        var defaults = KrackendMuleRuntimeCapacityProfile.Resolve(_runtimeOptions).Artifact;
        EnsureLane(options, RuntimeArtifactProjectionSchedulerDefaults.Lane, priority: 100, weight: 2, _runtimeOptions, defaults);
        EnsureLane(options, _replicaIdentity.LocalStandupLane, priority: 100, weight: 2, _runtimeOptions, defaults);
    }

    private static void EnsureLane(
        MuleSettings settings,
        string lane,
        int priority,
        int weight,
        KrackendOrchestrationsMuleRuntimeOptions runtimeOptions,
        ResolvedMuleLaneOptions defaults)
    {
        if (settings.Lanes.ContainsKey(lane))
        {
            return;
        }

        settings.Lanes[lane] = new MuleLaneSettings
        {
            Priority = priority,
            Weight = weight,
            WorkerCount = Math.Max(1, runtimeOptions.ArtifactLifecycleWorkerCount ?? defaults.WorkerCount),
            MaxDegreeOfParallelism = Math.Max(1, runtimeOptions.ArtifactLifecycleMaxDegreeOfParallelism ?? defaults.MaxDegreeOfParallelism),
            DispatchBatchSize = Math.Max(1, runtimeOptions.ArtifactLifecycleDispatchBatchSize ?? defaults.DispatchBatchSize),
            DispatchQueueCapacity = Math.Max(0, runtimeOptions.ArtifactLifecycleDispatchQueueCapacity ?? defaults.DispatchQueueCapacity),
            ExecutionQueueCapacity = Math.Max(0, runtimeOptions.ArtifactLifecycleExecutionQueueCapacity ?? defaults.ExecutionQueueCapacity),
            MaxDrainBatchesPerCycle = Math.Max(1, runtimeOptions.ArtifactLifecycleMaxDrainBatchesPerCycle ?? defaults.MaxDrainBatchesPerCycle),
            MaxDrainActionsPerCycle = Math.Max(0, runtimeOptions.ArtifactLifecycleMaxDrainActionsPerCycle ?? defaults.MaxDrainActionsPerCycle),
            DrainUntilEmpty = runtimeOptions.ArtifactLifecycleDrainUntilEmpty ?? defaults.DrainUntilEmpty,
            MaxAttempts = Math.Max(1, runtimeOptions.ArtifactLifecycleMaxAttempts ?? 120),
            RetryDelay = runtimeOptions.ArtifactLifecycleRetryDelay is { } retryDelay && retryDelay > TimeSpan.Zero
                ? retryDelay
                : TimeSpan.FromSeconds(1)
        };
    }
}
