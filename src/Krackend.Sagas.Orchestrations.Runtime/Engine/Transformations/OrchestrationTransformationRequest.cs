namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;

/// <summary>
/// Describes a runtime transformation request for a task dispatch payload.
/// </summary>
public sealed record OrchestrationTransformationRequest
{
    /// <summary>
    /// Gets the task that owns the transformation.
    /// </summary>
    public required TaskArtifact Task { get; init; }

    /// <summary>
    /// Gets the accumulated payload context.
    /// </summary>
    public required OrchestrationPayloadContext PayloadContext { get; init; }
}
