namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>Diagnoses and repairs recoverable work for one active orchestration instance.</summary>
public interface IOrchestrationReconciliationService
{
    /// <summary>Reconciles an instance under its coordination lease.</summary>
    Task<OrchestrationReconciliationResult> ReconcileAsync(string instanceId, CancellationToken cancellationToken = default);
}

/// <summary>One observed condition and the action taken for it.</summary>
public sealed record OrchestrationReconciliationFinding(string Status, string Message);

/// <summary>Outcome of an operator-requested reconciliation.</summary>
public sealed record OrchestrationReconciliationResult(string Status, IReadOnlyList<OrchestrationReconciliationFinding> Findings);
