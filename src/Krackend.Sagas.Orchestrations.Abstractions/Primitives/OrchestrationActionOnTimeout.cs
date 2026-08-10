namespace Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Defines how orchestration flow should react after a timeout policy is applied.
/// </summary>
public enum OrchestrationActionOnTimeout
{
    /// <summary>
    /// Blocks progression until manual intervention or a later retry strategy resolves the timeout.
    /// </summary>
    Block,

    /// <summary>
    /// Continues orchestration flow despite the timeout.
    /// </summary>
    Continue
}
