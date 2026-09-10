namespace Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents an immutable payload validation contract inside an orchestration artifact.
/// </summary>
public sealed record ValidationArtifact(
    EngineType Engine,
    IValidationConfigurationArtifact Configuration)
{
    /// <summary>
    /// Gets whether this validation should be executed.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the orchestration error code reported when validation fails without a more specific code.
    /// </summary>
    public string ErrorCode { get; init; } = "PayloadValidationFailed";
}
