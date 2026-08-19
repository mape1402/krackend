using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Repositories;

/// <summary>
/// Reads runtime artifacts directly from EF Core storage for runtime synchronization paths.
/// </summary>
public sealed class EfCoreRuntimeArtifactCatalog : IRuntimeArtifactCatalog
{
    private const string DeployArtifactType = "orchestration.deploy";

    private readonly RuntimeStorageDbContext _dbContext;

    public EfCoreRuntimeArtifactCatalog(RuntimeStorageDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<RuntimeArtifactPage> ReadActiveDeployments(
        RuntimeArtifactPageCursor cursor,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");

        var offset = Math.Max(0, cursor?.Offset ?? 0);
        var rows = await _dbContext.Artifacts
            .AsNoTracking()
            .Where(x => x.IsActive && x.ArtifactType == DeployArtifactType)
            .OrderByDescending(x => x.ActivatedOnUtc ?? x.DeployedOnUtc)
            .ThenBy(x => x.OrchestrationDefinitionKey)
            .Skip(offset)
            .Take(pageSize + 1)
            .ToArrayAsync(cancellationToken);

        var hasMore = rows.Length > pageSize;
        var items = rows.Take(pageSize).Select(RuntimeStorageMapper.ToDomain).ToArray();
        var nextCursor = hasMore ? new RuntimeArtifactPageCursor(offset + items.Length) : null;

        return new RuntimeArtifactPage(items, nextCursor, hasMore);
    }
}
