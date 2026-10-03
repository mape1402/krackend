namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Builds runtime dispatch commands for a supported orchestration task kind.
/// </summary>
public interface ITaskRuntimeAdapter
{
    /// <summary>
    /// Gets the task kind supported by the adapter.
    /// </summary>
    TaskKind TaskKind { get; }

    /// <summary>
    /// Validates a task artifact against the adapter capabilities.
    /// </summary>
    RuntimeArtifactCompatibilityValidationResult ValidateTask(TaskArtifact task, string stageKey);

    /// <summary>
    /// Validates a compensation artifact against the adapter capabilities.
    /// </summary>
    RuntimeArtifactCompatibilityValidationResult ValidateCompensation(TaskArtifact task, string stageKey);

    /// <summary>
    /// Gets the persisted dispatch destination for the task.
    /// </summary>
    string GetDestination(TaskArtifact task);

    /// <summary>
    /// Gets the persisted dispatch destination for the compensation task.
    /// </summary>
    string GetCompensationDestination(CompensationArtifact compensation);

    /// <summary>
    /// Builds a remote command for a task attempt.
    /// </summary>
    Task<TaskRuntimeCommandDescriptor> BuildCommandAsync(
        TaskRuntimeCommandRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a remote command for a compensation task.
    /// </summary>
    Task<TaskRuntimeCommandDescriptor> BuildCompensationCommandAsync(
        TaskRuntimeCompensationCommandRequest request,
        CancellationToken cancellationToken = default);
}
