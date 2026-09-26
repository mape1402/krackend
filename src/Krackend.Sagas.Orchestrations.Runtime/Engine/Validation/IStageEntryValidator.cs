namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Validates whether a stage can start with the currently accumulated orchestration payload context.
/// </summary>
public interface IStageEntryValidator
{
    /// <summary>
    /// Validates the configured entry rules for a stage.
    /// </summary>
    /// <param name="stage">Stage artifact to validate.</param>
    /// <param name="payloadContext">Accumulated payload context available at stage entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<OrchestrationValidationResult> ValidateAsync(
        StageArtifact stage,
        OrchestrationPayloadContext payloadContext,
        CancellationToken cancellationToken = default);
}
