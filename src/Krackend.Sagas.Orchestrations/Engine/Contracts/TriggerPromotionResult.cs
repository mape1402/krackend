using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Result returned when a trigger intake item is promoted into runtime execution state.
/// </summary>
public sealed class TriggerPromotionResult
{
    /// <summary>
    /// Gets the persisted runtime intake entry.
    /// </summary>
    public required TriggerIntake Intake { get; init; }

    /// <summary>
    /// Gets the orchestration instance created for the trigger.
    /// </summary>
    public required OrchestrationInstance Instance { get; init; }

    /// <summary>
    /// Gets the runtime artifact used to create the instance.
    /// </summary>
    public required RuntimeOrchestrationArtifact Artifact { get; init; }
}
