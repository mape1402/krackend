namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Promotion;

using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using System.Text.Json.Nodes;

/// <summary>
/// Validates trigger payloads before they are promoted into orchestration instances.
/// </summary>
internal interface ITriggerPayloadValidator
{
    /// <summary>
    /// Validates the trigger payload against the resolved orchestration artifact.
    /// </summary>
    /// <param name="artifact">Resolved orchestration artifact.</param>
    /// <param name="payload">Business payload received by the trigger ingress.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Validation result.</returns>
    Task<OrchestrationValidationResult> ValidateAsync(
        ResolvedOrchestrationArtifact artifact,
        JsonNode payload,
        CancellationToken cancellationToken = default);
}
