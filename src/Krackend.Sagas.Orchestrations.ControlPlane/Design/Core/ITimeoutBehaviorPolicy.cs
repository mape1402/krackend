namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines the timeout behavior contract used by timeout policies in design-time models.
/// </summary>
public interface ITimeoutBehaviorPolicy
{
    /// <summary>
    /// Gets the timeout behavior represented by this policy.
    /// </summary>
    TimeoutBehavior Behavior { get; }
}
