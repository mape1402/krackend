namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>Provides bounded, searchable design-node listings for runtime administration.</summary>
public interface IRuntimeDesignNodeUiCatalog
{
    /// <summary>Gets one page of matching design nodes.</summary>
    Task<RuntimeDesignNodePage> GetPage(int page, int pageSize, string search, CancellationToken cancellationToken = default);
}

/// <summary>A page of runtime design nodes and its total matching count.</summary>
public sealed record RuntimeDesignNodePage(IReadOnlyCollection<RuntimeDesignNode> Items, long TotalCount, int PageNumber, int PageSize);
