namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Configures Mule lanes owned by the Krackend orchestration runtime.
/// </summary>
public sealed class KrackendOrchestrationsMuleRuntimeOptions
{
    /// <summary>
    /// Gets or sets the number of workers assigned to artifact projection and local standup lanes.
    /// </summary>
    public int ArtifactLifecycleWorkerCount { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum number of artifact lifecycle actions that can execute in parallel.
    /// </summary>
    public int ArtifactLifecycleMaxDegreeOfParallelism { get; set; } = 256;

    /// <summary>
    /// Gets or sets the number of pending artifact lifecycle actions claimed per dispatch batch.
    /// </summary>
    public int ArtifactLifecycleDispatchBatchSize { get; set; } = 250;

    /// <summary>
    /// Gets or sets the dispatch queue capacity for artifact lifecycle lanes; zero uses Mule's unbounded queue.
    /// </summary>
    public int ArtifactLifecycleDispatchQueueCapacity { get; set; }

    /// <summary>
    /// Gets or sets the execution queue capacity for artifact lifecycle lanes; zero uses Mule's unbounded queue.
    /// </summary>
    public int ArtifactLifecycleExecutionQueueCapacity { get; set; }

    /// <summary>
    /// Gets or sets the maximum drain batches per recovery cycle for artifact lifecycle lanes.
    /// </summary>
    public int ArtifactLifecycleMaxDrainBatchesPerCycle { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum drain actions per recovery cycle for artifact lifecycle lanes; zero means no action limit.
    /// </summary>
    public int ArtifactLifecycleMaxDrainActionsPerCycle { get; set; }

    /// <summary>
    /// Gets or sets whether artifact lifecycle lanes should keep draining until no pending actions remain.
    /// </summary>
    public bool ArtifactLifecycleDrainUntilEmpty { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum retry attempts for transient artifact lifecycle failures.
    /// </summary>
    public int ArtifactLifecycleMaxAttempts { get; set; } = 120;

    /// <summary>
    /// Gets or sets the delay between artifact lifecycle retry attempts.
    /// </summary>
    public TimeSpan ArtifactLifecycleRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
