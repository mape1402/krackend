namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents the complete diagnostic detail for one orchestration instance.
/// </summary>
public sealed record InstanceDetailModel(
    InstanceRowModel Instance,
    IReadOnlyCollection<StageDetailModel> Stages,
    IReadOnlyCollection<TaskDetailModel> Tasks,
    IReadOnlyCollection<TransitionDetailModel> Transitions,
    string SnapshotPayload,
    string Metadata,
    IReadOnlyCollection<CompensationDetailModel> Compensations,
    IReadOnlyCollection<RuntimeTimelineEntryModel> FunctionalTimeline,
    IReadOnlyCollection<RuntimeTimelineEntryModel> TechnicalTimeline)
{
    /// <summary>Whether the deployed artifact was available for this diagnostic view.</summary>
    public bool? ArtifactAvailable { get; init; }
}
