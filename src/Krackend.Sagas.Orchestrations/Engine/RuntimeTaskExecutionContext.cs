using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Carries task execution inputs while the engine processes a task.
/// </summary>
internal sealed class RuntimeTaskExecutionContext
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
    /// Gets the parent stage execution.
    /// </summary>
    public required StageExecution StageExecution { get; init; }

    /// <summary>
    /// Gets the runtime task definition.
    /// </summary>
    public required RuntimeTaskDocument Task { get; init; }
}
