#nullable enable

namespace Krackend.Sagas.Orchestrations.Runtime.Recovery;

/// <summary>
/// Summarizes one reconciliation run.
/// </summary>
public sealed record RuntimeReconciliationResult(
    int InstancesObserved,
    int InstancesAdvanced,
    int TimeoutsProcessed,
    bool Skipped,
    string? Reason = null);
