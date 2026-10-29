namespace Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;

/// <summary>
/// Configures the runtime-owned Mule topology. A balanced profile is applied automatically;
/// individual lane settings are reserved for advanced host-specific tuning.
/// </summary>
public sealed class KrackendOrchestrationsMuleRuntimeOptions
{
    /// <summary>Gets or sets the recommended capacity profile used when a lane is not overridden.</summary>
    public KrackendMuleRuntimeProfile Profile { get; set; } = KrackendMuleRuntimeProfile.Balanced;

    /// <summary>Gets or sets the total durable-store claim workers shared by all runtime-owned lanes.</summary>
    public int? TotalWorkerCount { get; set; }

    /// <summary>Gets or sets the total concurrently executing runtime actions shared by all runtime-owned lanes.</summary>
    public int? TotalMaxDegreeOfParallelism { get; set; }

    /// <summary>Gets the lane settings for trigger intake and orchestration start actions.</summary>
    public KrackendOrchestrationsMuleLaneOptions Trigger { get; } = new();

    /// <summary>Gets the lane settings for service callbacks, which unblock waiting orchestrations.</summary>
    public KrackendOrchestrationsMuleLaneOptions Backchannel { get; } = new();

    /// <summary>Gets the lane settings for outbound task dispatches.</summary>
    public KrackendOrchestrationsMuleLaneOptions Dispatch { get; } = new();

    /// <summary>Gets the lane settings for response-timeout actions.</summary>
    public KrackendOrchestrationsMuleLaneOptions Timeout { get; } = new();

    /// <summary>Gets the lane settings for deferred dashboard statistics projection.</summary>
    public KrackendOrchestrationsMuleLaneOptions Statistics { get; } = new();

    /// <summary>Gets or sets artifact lifecycle durable-store readers; null uses the selected profile.</summary>
    public int? ArtifactLifecycleWorkerCount { get; set; }

    /// <summary>Gets or sets artifact lifecycle action concurrency; null uses the selected profile.</summary>
    public int? ArtifactLifecycleMaxDegreeOfParallelism { get; set; }

    /// <summary>Gets or sets artifact lifecycle durable claim batch size.</summary>
    public int? ArtifactLifecycleDispatchBatchSize { get; set; }

    /// <summary>Gets or sets artifact lifecycle dispatch queue capacity.</summary>
    public int? ArtifactLifecycleDispatchQueueCapacity { get; set; }

    /// <summary>Gets or sets artifact lifecycle execution queue capacity.</summary>
    public int? ArtifactLifecycleExecutionQueueCapacity { get; set; }

    /// <summary>Gets or sets maximum artifact drain batches before yielding.</summary>
    public int? ArtifactLifecycleMaxDrainBatchesPerCycle { get; set; }

    /// <summary>Gets or sets maximum artifact actions claimed before yielding.</summary>
    public int? ArtifactLifecycleMaxDrainActionsPerCycle { get; set; }

    /// <summary>Gets or sets whether artifact lanes continuously drain eligible work.</summary>
    public bool? ArtifactLifecycleDrainUntilEmpty { get; set; }

    /// <summary>Gets or sets maximum artifact lifecycle retry attempts.</summary>
    public int? ArtifactLifecycleMaxAttempts { get; set; }

    /// <summary>Gets or sets the delay between artifact lifecycle retry attempts.</summary>
    public TimeSpan? ArtifactLifecycleRetryDelay { get; set; }
}

/// <summary>Named capacity profiles supplied by Krackend for common runtime deployments.</summary>
public enum KrackendMuleRuntimeProfile
{
    /// <summary>Low-resource local development.</summary>
    Development,

    /// <summary>Production-safe default that balances durable-store pressure and throughput.</summary>
    Balanced,

    /// <summary>High-throughput profile for hosts provisioned for sustained messaging load.</summary>
    HighThroughput,

    /// <summary>Host provides all relevant capacity settings explicitly.</summary>
    Custom
}

/// <summary>Configures throughput and backpressure for one runtime-owned Mule lane.</summary>
public sealed class KrackendOrchestrationsMuleLaneOptions
{
    /// <summary>Gets or sets durable-storage readers that may claim work for the lane.</summary>
    public int? WorkerCount { get; set; }

    /// <summary>Gets or sets maximum already claimed actions that may execute concurrently.</summary>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>Gets or sets actions claimed in one durable-storage read.</summary>
    public int? DispatchBatchSize { get; set; }

    /// <summary>Gets or sets immediate dispatch queue capacity.</summary>
    public int? DispatchQueueCapacity { get; set; }

    /// <summary>Gets or sets claimed-action execution queue capacity.</summary>
    public int? ExecutionQueueCapacity { get; set; }

    /// <summary>Gets or sets maximum batches claimed before the worker yields.</summary>
    public int? MaxDrainBatchesPerCycle { get; set; }

    /// <summary>Gets or sets maximum actions claimed before the worker yields.</summary>
    public int? MaxDrainActionsPerCycle { get; set; }

    /// <summary>Gets or sets whether the lane continuously drains eligible work.</summary>
    public bool? DrainUntilEmpty { get; set; }
}
