namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Protects orchestration version composition from edits after it leaves Draft status.
/// </summary>
public interface IOrchestrationVersionEditGuard
{
    /// <summary>
    /// Ensures an orchestration version is editable.
    /// </summary>
    Task EnsureVersionIsDraft(string orchestrationVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a stage belongs to an editable orchestration version.
    /// </summary>
    Task EnsureStageVersionIsDraft(string stageDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a task belongs to an editable orchestration version.
    /// </summary>
    Task EnsureTaskVersionIsDraft(string taskDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a trigger belongs to an editable orchestration version.
    /// </summary>
    Task EnsureTriggerVersionIsDraft(string triggerBindingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a variable belongs to an editable orchestration version.
    /// </summary>
    Task EnsureVariableVersionIsDraft(string variableDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a parallel group belongs to an editable orchestration version.
    /// </summary>
    Task EnsureParallelGroupVersionIsDraft(string parallelGroupDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a branch rule belongs to an editable orchestration version.
    /// </summary>
    Task EnsureBranchRuleVersionIsDraft(string branchRuleDefinitionId, CancellationToken cancellationToken = default);
}
