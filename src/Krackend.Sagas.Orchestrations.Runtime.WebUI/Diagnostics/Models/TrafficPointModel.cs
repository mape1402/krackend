namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

/// <summary>
/// Represents bucketed runtime traffic for charting active and finished orchestration activity.
/// </summary>
public sealed record TrafficPointModel(
    DateTime BucketUtc,
    int Active,
    int Started,
    int Completed,
    int Failed);
