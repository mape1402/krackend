using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>Reads the deferred statistics projection without querying runtime instances.</summary>
public interface ISagaStatisticsReader
{
    /// <summary>Returns statistics for instances started within the requested UTC period.</summary>
    Task<SagaStatisticsReport> GetReportAsync(DateTime fromUtc, DateTime beforeUtc, string orchestrationKey = null,
        CancellationToken cancellationToken = default);
}

/// <summary>A daily count of observed instance status changes.</summary>
public sealed record SagaStatisticsActivity(DateTime DayUtc, OrchestrationInstanceStatus Status, long Count);

/// <summary>A count of projected instances belonging to an orchestration.</summary>
public sealed record SagaStatisticsOrchestration(string Key, long Count);

/// <summary>A report from the durable statistics projection.</summary>
public sealed record SagaStatisticsReport(
    DateTime FromUtc,
    DateTime BeforeUtc,
    string OrchestrationKey,
    long Total,
    IReadOnlyDictionary<OrchestrationInstanceStatus, long> CurrentStatuses,
    IReadOnlyDictionary<OrchestrationInstanceStatus, long> ReachedStatuses,
    IReadOnlyCollection<SagaStatisticsActivity> Activity,
    IReadOnlyCollection<SagaStatisticsOrchestration> Orchestrations,
    long PendingObservations,
    bool BackfillCompleted,
    DateTime? LastObservedOnUtc);
