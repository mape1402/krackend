namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Runtime timeout policy evaluated from the artifact.
/// </summary>
public sealed class RuntimeTimeoutPolicy
{
    /// <summary>
    /// Gets or sets timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets timeout behavior.
    /// </summary>
    public string Behavior { get; init; } = "Fail";

    /// <summary>
    /// Gets or sets orchestration action after wait/reconcile timeout.
    /// </summary>
    public string OrchestrationAction { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets waiting duration for wait policies.
    /// </summary>
    public TimeSpan WaitingTime { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets fail behavior error code.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether timeout handling is configured.
    /// </summary>
    public bool IsConfigured => Timeout > TimeSpan.Zero;
}
