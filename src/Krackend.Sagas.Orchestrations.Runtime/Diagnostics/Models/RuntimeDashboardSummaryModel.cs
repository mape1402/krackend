namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents runtime dashboard counters and traffic refresh data.
/// </summary>
public sealed record RuntimeDashboardSummaryModel(
    RuntimeSummaryModel Summary,
    IReadOnlyCollection<TrafficPointModel> Traffic,
    IReadOnlyCollection<TrafficPointModel> HourlyTraffic);
