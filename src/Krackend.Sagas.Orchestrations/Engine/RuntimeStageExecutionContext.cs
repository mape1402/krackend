using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Carries stage execution inputs while the engine processes a stage.
/// </summary>
internal sealed class RuntimeStageExecutionContext
{
    /// <summary>
    /// Gets the orchestration instance being processed.
    /// </summary>
    public required OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the orchestration artifact version.
    /// </summary>
    public required string OrchestrationVersion { get; init; }

    /// <summary>
    /// Gets the runtime stage definition.
    /// </summary>
    public required RuntimeStageDocument Stage { get; init; }
}
