namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>
/// Configures distributed coordination for orchestration instance mutations.
/// </summary>
public sealed class OrchestrationCoordinationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether instance-level coordination is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the lease duration, in seconds, for one orchestration instance mutation scope.
    /// </summary>
    public int LeaseDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long a runtime waits for a busy instance lease before skipping the operation.
    /// </summary>
    public int LeaseUnavailableWaitMilliseconds { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the delay between attempts while waiting for a busy instance lease.
    /// </summary>
    public int LeaseUnavailableRetryDelayMilliseconds { get; set; } = 25;
}
