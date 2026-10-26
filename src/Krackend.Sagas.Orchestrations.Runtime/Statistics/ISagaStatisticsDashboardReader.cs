using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Runtime.Statistics;

/// <summary>
/// Reads independently refreshable dashboard projections without querying runtime instances.
/// </summary>
public interface ISagaStatisticsDashboardReader
{
    /// <summary>Returns orchestration keys that have statistics projections.</summary>
    Task<IReadOnlyCollection<string>> GetOrchestrationKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the current statuses for one started-instance cohort.</summary>
    Task<SagaStatisticsDashboardCohort> GetCohortAsync(SagaStatisticsDashboardQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns status changes grouped in the requested UTC bucket size.</summary>
    Task<IReadOnlyCollection<SagaStatisticsDashboardActivity>> GetActivityAsync(SagaStatisticsDashboardQuery query,
        SagaStatisticsActivityBucket bucket, CancellationToken cancellationToken = default);

    /// <summary>Returns the independent projection freshness state.</summary>
    Task<SagaStatisticsDashboardHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>Identifies a UTC cohort of orchestration instances.</summary>
public sealed record SagaStatisticsDashboardQuery(DateTime FromUtc, DateTime BeforeUtc, string? OrchestrationKey = null)
{
    /// <summary>Returns a normalized query with a valid UTC range.</summary>
    public SagaStatisticsDashboardQuery Normalize()
    {
        if (FromUtc >= BeforeUtc)
        {
            throw new ArgumentException("The dashboard range start must precede its end.");
        }

        return this with
        {
            FromUtc = DateTime.SpecifyKind(FromUtc, DateTimeKind.Utc),
            BeforeUtc = DateTime.SpecifyKind(BeforeUtc, DateTimeKind.Utc),
            OrchestrationKey = string.IsNullOrWhiteSpace(OrchestrationKey) ? null : OrchestrationKey.Trim()
        };
    }
}

/// <summary>Defines the UTC grouping used by an activity chart.</summary>
public enum SagaStatisticsActivityBucket
{
    /// <summary>One activity point per UTC hour.</summary>
    Hour,

    /// <summary>One activity point per UTC day.</summary>
    Day,

    /// <summary>One activity point per UTC week beginning Monday.</summary>
    Week
}

/// <summary>Describes the exclusive current status of a started-instance cohort.</summary>
public sealed record SagaStatisticsDashboardCohort(
    DateTime FromUtc,
    DateTime BeforeUtc,
    string? OrchestrationKey,
    long Total,
    IReadOnlyDictionary<OrchestrationInstanceStatus, long> CurrentStatuses);

/// <summary>Describes activity observed in one UTC time bucket.</summary>
public sealed record SagaStatisticsDashboardActivity(
    DateTime BucketUtc,
    OrchestrationInstanceStatus Status,
    long Count);

/// <summary>Describes whether the deferred dashboard projection has caught up.</summary>
public sealed record SagaStatisticsDashboardHealth(
    long PendingObservations,
    bool BackfillCompleted,
    DateTime? LastObservedOnUtc);
