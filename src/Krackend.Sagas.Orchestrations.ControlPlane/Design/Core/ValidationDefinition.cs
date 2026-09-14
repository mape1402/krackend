namespace Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Represents payload validation behavior applied to trigger, request, or response payloads.
/// </summary>
public sealed class ValidationDefinition
{
    /// <summary>
    /// Gets or sets the validation engine.
    /// </summary>
    public required EngineType Engine { get; set; }

    /// <summary>
    /// Gets or sets the validation configuration.
    /// </summary>
    public IValidationConfiguration Configuration { get; set; }

    /// <summary>
    /// Gets or sets the orchestration error code used when validation fails without a specific code.
    /// </summary>
    public string ErrorCode { get; set; } = "PayloadValidationFailed";
}
