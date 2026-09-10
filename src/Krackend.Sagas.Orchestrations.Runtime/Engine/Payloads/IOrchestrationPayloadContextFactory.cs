namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Builds runtime payload contexts for transforms and validations.
/// </summary>
public interface IOrchestrationPayloadContextFactory
{
    /// <summary>
    /// Creates the accumulated payload context for a task dispatch.
    /// </summary>
    /// <param name="instance">Current orchestration instance.</param>
    /// <param name="stageKey">Current stage key.</param>
    /// <param name="taskKey">Current task key.</param>
    /// <returns>The accumulated payload context.</returns>
    OrchestrationPayloadContext Create(
        OrchestrationInstance instance,
        string stageKey,
        string taskKey);
}
