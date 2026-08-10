using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;

/// <summary>
/// Represents ParallelGroupRepository.
/// </summary>
public sealed class ParallelGroupRepository : IParallelGroupRepository
{
    private readonly DesignStorageDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public ParallelGroupRepository(DesignStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(ParallelGroupDefinition parallelGroupDefinition, CancellationToken cancellationToken = default)
    {
        _dbContext.ParallelGroupDefinitions.Add(parallelGroupDefinition.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(ParallelGroupDefinition parallelGroupDefinition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.ParallelGroupDefinitions.FirstOrDefaultAsync(x => x.Id == parallelGroupDefinition.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ParallelGroupDefinition '{parallelGroupDefinition.Id}' was not found.");

        var next = parallelGroupDefinition.ToEntity();
        _dbContext.Entry(current).CurrentValues.SetValues(next);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Delete.
    /// </summary>
    public async Task Delete(Id parallelGroupDefinitionId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.ParallelGroupDefinitions.FirstOrDefaultAsync(x => x.Id == parallelGroupDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"ParallelGroupDefinition '{parallelGroupDefinitionId}' was not found.");

        _dbContext.ParallelGroupDefinitions.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<IEnumerable<ParallelGroupDefinition>> GetAll(Id stageDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ParallelGroupDefinitions
            .AsNoTracking()
            .Where(x => x.StageDefinitionId == stageDefinitionId)
            .OrderBy(x => x.Id)
            .Select(x => x.ToDefinition())
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<ParallelGroupDefinition> GetById(Id parallelGroupDefinitionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ParallelGroupDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == parallelGroupDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"ParallelGroupDefinition '{parallelGroupDefinitionId}' was not found.");

        return entity.ToDefinition();
    }
}
