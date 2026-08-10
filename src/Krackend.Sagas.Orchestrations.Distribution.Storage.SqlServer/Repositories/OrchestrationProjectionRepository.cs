using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Entities;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Repositories;

public sealed class OrchestrationProjectionRepository : IOrchestrationProjectionRepository
{
    private readonly DistributionStorageDbContext _dbContext;

    public OrchestrationProjectionRepository(DistributionStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Upsert(OrchestrationProjection orchestration, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.OrchestrationProjections
            .FirstOrDefaultAsync(x => x.Id == orchestration.Id, cancellationToken);

        if (existing is null)
        {
            _dbContext.OrchestrationProjections.Add(new OrchestrationProjectionEntity
            {
                Id = orchestration.Id,
                Key = orchestration.Key,
                Name = orchestration.Name,
                IsActive = orchestration.IsActive,
                CreatedAtUtc = orchestration.CreatedAtUtc,
                UpdatedAtUtc = orchestration.UpdatedAtUtc
            });
        }
        else
        {
            existing.Key = orchestration.Key;
            existing.Name = orchestration.Name;
            existing.IsActive = orchestration.IsActive;
            existing.UpdatedAtUtc = orchestration.UpdatedAtUtc ?? DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrchestrationProjection>> GetAll(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.OrchestrationProjections
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Key)
            .ToArrayAsync(cancellationToken);

        return rows.Select(x => new OrchestrationProjection
        {
            Id = x.Id,
            Key = x.Key,
            Name = x.Name,
            IsActive = x.IsActive,
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        }).ToArray();
    }

    public Task<bool> Exists(string orchestrationId, CancellationToken cancellationToken = default)
        => _dbContext.OrchestrationProjections.AsNoTracking().AnyAsync(x => x.Id == orchestrationId, cancellationToken);
}


