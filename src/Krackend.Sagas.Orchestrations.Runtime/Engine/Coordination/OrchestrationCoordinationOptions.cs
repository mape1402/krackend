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
}
