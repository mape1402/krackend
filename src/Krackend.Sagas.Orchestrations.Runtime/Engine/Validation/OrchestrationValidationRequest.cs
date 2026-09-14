namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

/// <summary>
/// Describes a payload validation request.
/// </summary>
public sealed record OrchestrationValidationRequest
{
    /// <summary>
    /// Gets the task that owns the validation when validation is task-scoped.
    /// </summary>
    public TaskArtifact Task { get; init; }

    /// <summary>
    /// Gets the trigger that owns the validation when validation is trigger-scoped.
    /// </summary>
    public TriggerBindingArtifact Trigger { get; init; }

    /// <summary>
    /// Gets the schema binding used to validate the payload.
    /// </summary>
    public SchemaBindingArtifact SchemaBinding { get; init; }

    /// <summary>
    /// Gets the business payload to validate.
    /// </summary>
    public JsonNode Payload { get; init; }

    /// <summary>
    /// Gets the validation DSL to execute when explicit validation rules are configured.
    /// </summary>
    public string ValidationDsl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the logical validation phase.
    /// </summary>
    public required string Phase { get; init; }
}
