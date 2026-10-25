using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>Reads paged instance diagnostics from the configured runtime storage.</summary>
public interface IRuntimeInstanceCatalogReader
{
    /// <summary>Returns one page of matching instance rows.</summary>
    Task<RuntimeInstancePageModel> GetInstances(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>A page of runtime instance diagnostic rows.</summary>
public sealed record RuntimeInstancePageModel(IReadOnlyCollection<InstanceRowModel> Items, long TotalCount, int PageNumber, int PageSize);
