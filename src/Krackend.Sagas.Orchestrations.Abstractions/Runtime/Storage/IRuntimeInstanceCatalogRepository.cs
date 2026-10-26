using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>Queries runtime instances for diagnostic catalogs without loading the complete history.</summary>
public interface IRuntimeInstanceCatalogRepository
{
    /// <summary>Returns a filtered, deterministically ordered page of instances.</summary>
    Task<RuntimeInstanceCatalogPage> GetPage(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Counts current instance states within a diagnostic catalog scope.</summary>
public interface IRuntimeInstanceStatusCountsRepository
{
    /// <summary>Returns counts for the same filters as the catalog, independent of paging.</summary>
    Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(
        RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Criteria used by a runtime instance catalog.</summary>
public sealed record RuntimeInstanceCatalogQuery(
    int PageNumber = 1,
    int PageSize = 25,
    string SearchText = "",
    OrchestrationInstanceStatus? Status = null,
    RuntimeInstanceCatalogSort Sort = RuntimeInstanceCatalogSort.Updated,
    bool Descending = true,
    DateTime? StartedFromUtc = null,
    DateTime? StartedBeforeUtc = null,
    bool ActiveOnly = false);

/// <summary>Supported instance ordering fields.</summary>
public enum RuntimeInstanceCatalogSort { Updated, Started, Orchestration, CorrelationId, SagaId, Status }

/// <summary>One page and the number of matching instances.</summary>
public sealed record RuntimeInstanceCatalogPage(IReadOnlyCollection<OrchestrationInstance> Items, long TotalCount, int PageNumber, int PageSize);
