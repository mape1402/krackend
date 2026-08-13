namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

public interface IRuntimeDiagnosticsReader
{
    Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default);

    Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default);

    Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default);
}
