namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

/// <summary>
/// Reads runtime diagnostics projections for dashboard and trace views.
/// </summary>
public interface IRuntimeDiagnosticsReader
{
    /// <summary>
    /// Gets a full runtime dashboard snapshot.
    /// </summary>
    Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets runtime counters and traffic series.
    /// </summary>
    Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a full trace for an orchestration instance.
    /// </summary>
    Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default);
}
