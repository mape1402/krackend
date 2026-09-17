namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents aggregate runtime counters shown above the orchestration instance grid.
/// </summary>
public sealed record RuntimeSummaryModel(
    int Active,
    int Waiting,
    int CompletedLastMinute,
    int FailedLastMinute,
    int CompletedLastHour,
    int FailedLastHour,
    DateTime MinuteSinceUtc,
    DateTime HourSinceUtc);
