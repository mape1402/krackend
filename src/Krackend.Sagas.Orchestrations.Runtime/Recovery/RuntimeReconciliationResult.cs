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
    string? Reason = null,
    int DeliveriesRecovered = 0,
    int CompensationsRecovered = 0,
    int DispatchCancellationsReconciled = 0)
{
    /// <summary>Preserves the original reconciliation result constructor.</summary>
    public RuntimeReconciliationResult(
        int InstancesObserved,
        int InstancesAdvanced,
        int TimeoutsProcessed,
        bool Skipped,
        string? Reason,
        int DeliveriesRecovered,
        int CompensationsRecovered)
        : this(InstancesObserved, InstancesAdvanced, TimeoutsProcessed, Skipped, Reason,
            DeliveriesRecovered, CompensationsRecovered, 0)
    {
    }

    /// <summary>Deconstructs the original reconciliation result fields.</summary>
    public void Deconstruct(out int instancesObserved, out int instancesAdvanced, out int timeoutsProcessed,
        out bool skipped, out string? reason, out int deliveriesRecovered, out int compensationsRecovered)
        => (instancesObserved, instancesAdvanced, timeoutsProcessed, skipped, reason, deliveriesRecovered, compensationsRecovered)
            = (InstancesObserved, InstancesAdvanced, TimeoutsProcessed, Skipped, Reason, DeliveriesRecovered, CompensationsRecovered);
}
