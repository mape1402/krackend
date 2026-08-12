namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Actions the runtime can apply after a terminal task failure.
/// </summary>
public enum RuntimeErrorPolicyAction
{
    /// <summary>
    /// Continue with the next configured step.
    /// </summary>
    Continue,

    /// <summary>
    /// Stop the current stage and fail the instance.
    /// </summary>
    Stop,

    /// <summary>
    /// Start compensation for completed tasks.
    /// </summary>
    StartCompensation
}
