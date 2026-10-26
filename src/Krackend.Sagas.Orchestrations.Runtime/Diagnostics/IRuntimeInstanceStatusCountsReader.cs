using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>Reads current status counts for a scoped instance catalog.</summary>
public interface IRuntimeInstanceStatusCountsReader
{
    /// <summary>Returns counts without applying pagination.</summary>
    Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(
        RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default);
}
