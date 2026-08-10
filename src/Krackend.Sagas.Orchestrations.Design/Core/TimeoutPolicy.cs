namespace Krackend.Sagas.Orchestrations.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents timeout behavior configuration applied to stage or task execution.
/// </summary>
public sealed class TimeoutPolicy
{
    /// <summary>
    /// Gets or sets timeout.
    /// </summary>
    public Duration Timeout { get; set; }

    /// <summary>
    /// Gets or sets timeout behavior.
    /// </summary>
    public TimeoutBehavior TimeoutBehavior { get; set; }

    /// <summary>
    /// Gets or sets the policy that defines the behavior for handling timeouts during operations.
    /// </summary>
    /// <remarks>This property allows customization of timeout behavior, enabling developers to specify how
    /// the system should respond when a timeout occurs. It is important to configure this policy according to the
    /// specific requirements of the application to ensure optimal performance and error handling.</remarks>
    public ITimeoutBehaviorPolicy TimeoutBehaviorPolicy { get; set; }
}
