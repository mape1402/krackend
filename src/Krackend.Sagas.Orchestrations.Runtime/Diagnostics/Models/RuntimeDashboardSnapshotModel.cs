namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents the initial runtime dashboard data payload.
/// </summary>
public sealed record RuntimeDashboardSnapshotModel(
    RuntimeSummaryModel Summary,
    IReadOnlyCollection<InstanceRowModel> Instances,
    IReadOnlyCollection<TrafficPointModel> Traffic,
    IReadOnlyCollection<TrafficPointModel> HourlyTraffic);
