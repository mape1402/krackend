namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Canonical durable request for runtime reconciliation scans.
/// </summary>
public sealed class RuntimeReconcileRequest
{
    public required string ReconcileKey { get; init; }

    public DateTime DueOnUtc { get; init; }

    public string RequestedBy { get; init; } = "Krackend.Sagas.Orchestrations.RuntimeRecoveryHostedService";
}
