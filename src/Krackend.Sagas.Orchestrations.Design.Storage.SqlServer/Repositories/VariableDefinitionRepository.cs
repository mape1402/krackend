using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;

/// <summary>
/// Represents VariableDefinitionRepository.
/// </summary>
public sealed class VariableDefinitionRepository : IVariableDefinitionRepository
{
    private readonly DesignStorageDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public VariableDefinitionRepository(DesignStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(VariableDefinition variableDefinition, CancellationToken cancellationToken = default)
    {
        _dbContext.VariableDefinitions.Add(variableDefinition.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(VariableDefinition variableDefinition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.VariableDefinitions.FirstOrDefaultAsync(x => x.Id == variableDefinition.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"VariableDefinition '{variableDefinition.Id}' was not found.");

        var next = variableDefinition.ToEntity();
        _dbContext.Entry(current).CurrentValues.SetValues(next);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Delete.
    /// </summary>
    public async Task Delete(Id variableDefinitionId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.VariableDefinitions.FirstOrDefaultAsync(x => x.Id == variableDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"VariableDefinition '{variableDefinitionId}' was not found.");

        _dbContext.VariableDefinitions.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<IEnumerable<VariableDefinition>> GetAll(Id orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.VariableDefinitions
            .AsNoTracking()
            .Where(x => x.OrchestrationVersionId == orchestrationVersionId)
            .OrderBy(x => x.Key)
            .Select(x => x.ToDefinition())
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<VariableDefinition> GetById(Id variableDefinitionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.VariableDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == variableDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"VariableDefinition '{variableDefinitionId}' was not found.");

        return entity.ToDefinition();
    }
}
