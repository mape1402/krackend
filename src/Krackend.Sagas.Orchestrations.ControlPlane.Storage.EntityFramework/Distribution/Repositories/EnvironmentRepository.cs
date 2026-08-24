using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Entities;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Sieve.Models;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;

public sealed class EnvironmentRepository : IEnvironmentRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    public EnvironmentRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    public async Task Create(RuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        _dbContext.Environments.Add(Map(environment));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(RuntimeEnvironment environment, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Environments.FirstAsync(x => x.Id == environment.Id, cancellationToken);
        entity.Name = environment.Name;
        entity.Code = environment.Code;
        entity.Description = environment.Description;
        entity.IsEnabled = environment.IsEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetIsEnabled(Id environmentId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Environments.FirstAsync(x => x.Id == environmentId, cancellationToken);
        entity.IsEnabled = isEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RuntimeEnvironment> GetById(Id environmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Environments.AsNoTracking().FirstAsync(x => x.Id == environmentId, cancellationToken);
        return Map(entity);
    }

    public async Task<PagedResult<RuntimeEnvironment>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Environments.AsNoTracking().OrderBy(x => x.Name);
        var processed = _sieveProcessor.Apply(new SieveModel { Page = pagedSettings.PageNumber, PageSize = pagedSettings.PageSize }, query);
        var rows = await processed.ToArrayAsync(cancellationToken);
        var totalRows = await query.CountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pagedSettings.PageSize);
        return new PagedResult<RuntimeEnvironment>(pagedSettings.PageNumber, totalPages, totalRows, pagedSettings.PageSize, rows.Select(Map).ToArray());
    }

    private static EnvironmentEntity Map(RuntimeEnvironment x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Code = x.Code,
        Description = x.Description,
        IsEnabled = x.IsEnabled,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };

    private static RuntimeEnvironment Map(EnvironmentEntity x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Code = x.Code,
        Description = x.Description,
        IsEnabled = x.IsEnabled,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };
}

