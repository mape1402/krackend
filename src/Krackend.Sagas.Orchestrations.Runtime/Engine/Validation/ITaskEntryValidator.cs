namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Validates whether a task can be dispatched with the currently accumulated orchestration payload context.
/// </summary>
public interface ITaskEntryValidator
{
    /// <summary>
    /// Validates the configured entry rules for a task.
    /// </summary>
    /// <param name="task">Task artifact to validate.</param>
    /// <param name="payloadContext">Accumulated payload context available at task entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<OrchestrationValidationResult> ValidateAsync(
        TaskArtifact task,
        OrchestrationPayloadContext payloadContext,
        CancellationToken cancellationToken = default);
}
