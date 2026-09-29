using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

/// <summary>
/// Persists and queries orchestration metadata descriptors.
/// </summary>
public sealed class MetadataDescriptorRepository : IMetadataDescriptorRepository
{
    private readonly ControlPlaneDbContext _dbContext;
    private readonly ISieveProcessor _sieveProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataDescriptorRepository"/> class.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="sieveProcessor">Sieve processor dependency.</param>
    public MetadataDescriptorRepository(ControlPlaneDbContext dbContext, ISieveProcessor sieveProcessor)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sieveProcessor = sieveProcessor ?? throw new ArgumentNullException(nameof(sieveProcessor));
    }

    /// <inheritdoc />
    public async Task Upsert(MetadataDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var exists = await _dbContext.MetadataDescriptors.AnyAsync(x => x.Id == descriptor.Id, cancellationToken);
        if (!exists)
        {
            _dbContext.MetadataDescriptors.Add(descriptor.ToEntity());
        }
        else
        {
            var current = await _dbContext.MetadataDescriptors.FirstAsync(x => x.Id == descriptor.Id, cancellationToken);
            var next = descriptor.ToEntity();
            _dbContext.Entry(current).CurrentValues.SetValues(next);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task Delete(Id descriptorId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.MetadataDescriptors.FirstOrDefaultAsync(x => x.Id == descriptorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Metadata descriptor '{descriptorId}' was not found.");

        _dbContext.MetadataDescriptors.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MetadataDescriptor> GetById(Id descriptorId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.MetadataDescriptors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == descriptorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Metadata descriptor '{descriptorId}' was not found.");

        return entity.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<MetadataDescriptor> GetByKey(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalizedKey = key.Trim();
        var entity = await _dbContext.MetadataDescriptors.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);

        return entity?.ToDefinition();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MetadataDescriptor>> GetAllDescriptors(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.MetadataDescriptors.AsNoTracking()
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken);

        return rows.Select(x => x.ToDefinition()).ToArray();
    }

    /// <inheritdoc />
    public async Task<PagedResult<MetadataDescriptor>> GetAll(
        PagedSettings pagedSettings,
        string searchText = "",
        CancellationToken cancellationToken = default)
    {
        var sieveModel = pagedSettings.ToSieveModel();
        var query = _dbContext.MetadataDescriptors.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Key.ToLower().Contains(term) ||
                x.SourceKey.ToLower().Contains(term) ||
                x.DisplayName.ToLower().Contains(term) ||
                x.Description.ToLower().Contains(term));
        }

        var filteredAndSortedQuery = _sieveProcessor.Apply(sieveModel, query, applyPagination: false);
        if (string.IsNullOrWhiteSpace(sieveModel.Sorts))
        {
            filteredAndSortedQuery = filteredAndSortedQuery.OrderBy(x => x.Key);
        }
        var totalRows = await filteredAndSortedQuery.LongCountAsync(cancellationToken);
        var pageNumber = sieveModel.Page ?? 1;
        var pageSize = sieveModel.PageSize ?? 25;
        var totalPages = (int)Math.Ceiling((double)totalRows / pageSize);

        var rows = await _sieveProcessor.Apply(
                sieveModel,
                filteredAndSortedQuery,
                applyFiltering: false,
                applySorting: false,
                applyPagination: true)
            .ToListAsync(cancellationToken);

        return new PagedResult<MetadataDescriptor>(
            pageNumber,
            totalPages,
            totalRows,
            pageSize,
            rows.Select(x => x.ToDefinition()));
    }
}
