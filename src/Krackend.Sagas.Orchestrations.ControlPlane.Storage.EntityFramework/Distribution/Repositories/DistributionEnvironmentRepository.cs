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

/// <summary>
/// Entity Framework implementation of distribution environment persistence.
/// </summary>
public sealed class DistributionEnvironmentRepository : IDistributionEnvironmentRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributionEnvironmentRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Control Plane database context.</param>
    /// <param name="sieveProcessor">Sieve processor used for paging.</param>
    public DistributionEnvironmentRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext;
        _sieveProcessor = sieveProcessor;
    }

    /// <inheritdoc />
    public async Task Create(DistributionEnvironment environment, CancellationToken cancellationToken = default)
    {
        await EnsureCodeIsUnique(environment, cancellationToken);
        _dbContext.DistributionEnvironments.Add(Map(environment));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task Update(DistributionEnvironment environment, CancellationToken cancellationToken = default)
    {
        await EnsureCodeIsUnique(environment, cancellationToken);
        var entity = await _dbContext.DistributionEnvironments.FirstAsync(x => x.Id == environment.Id, cancellationToken);
        entity.Name = environment.Name;
        entity.Code = environment.Code;
        entity.Description = environment.Description;
        entity.IsEnabled = environment.IsEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DistributionEnvironment> GetById(Id environmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DistributionEnvironments
            .AsNoTracking()
            .FirstAsync(x => x.Id == environmentId, cancellationToken);
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<DistributionEnvironment> GetByCode(string code, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.DistributionEnvironments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    /// <inheritdoc />
    public async Task<PagedResult<DistributionEnvironment>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DistributionEnvironments.AsNoTracking().OrderBy(x => x.Name);
        var processed = _sieveProcessor.Apply(new SieveModel { Page = pagedSettings.PageNumber, PageSize = pagedSettings.PageSize }, query);
        var rows = await processed.ToArrayAsync(cancellationToken);
        var totalRows = await query.CountAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 1 : (int)Math.Ceiling(totalRows / (double)pagedSettings.PageSize);
        return new PagedResult<DistributionEnvironment>(pagedSettings.PageNumber, totalPages, totalRows, pagedSettings.PageSize, rows.Select(Map).ToArray());
    }

    private async Task EnsureCodeIsUnique(DistributionEnvironment environment, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.DistributionEnvironments
            .AsNoTracking()
            .AnyAsync(x => x.Id != environment.Id && x.Code == environment.Code, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Environment code '{environment.Code}' is already registered.");
        }
    }

    private static DistributionEnvironmentEntity Map(DistributionEnvironment x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Code = x.Code,
        Description = x.Description,
        IsEnabled = x.IsEnabled,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };

    private static DistributionEnvironment Map(DistributionEnvironmentEntity x) => new()
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
