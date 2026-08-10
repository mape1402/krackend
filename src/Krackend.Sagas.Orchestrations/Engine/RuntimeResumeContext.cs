namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Carries the parsed runtime artifact location where execution must resume.
/// </summary>
internal sealed class RuntimeResumeContext
{
    /// <summary>
    /// Gets the runtime artifact document.
    /// </summary>
    public required RuntimeArtifactDocument Document { get; init; }

    /// <summary>
    /// Gets the current runtime stage document.
    /// </summary>
    public required RuntimeStageDocument CurrentStage { get; init; }

    /// <summary>
    /// Gets the zero-based current stage index.
    /// </summary>
    public int StageIndex { get; init; }

    /// <summary>
    /// Gets the zero-based current task index.
    /// </summary>
    public int TaskIndex { get; init; }
}
