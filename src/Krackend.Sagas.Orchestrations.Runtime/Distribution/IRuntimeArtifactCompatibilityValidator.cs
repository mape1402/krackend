namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

using System.Text.Json.Nodes;

/// <summary>
/// Validates whether a deployable artifact can be interpreted by the configured runtime capabilities.
/// </summary>
public interface IRuntimeArtifactCompatibilityValidator
{
    /// <summary>
    /// Validates runtime compatibility for an artifact payload.
    /// </summary>
    /// <param name="artifactPayload">Artifact JSON payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compatibility validation result.</returns>
    Task<RuntimeArtifactCompatibilityValidationResult> ValidateAsync(
        JsonNode artifactPayload,
        CancellationToken cancellationToken = default);
}
