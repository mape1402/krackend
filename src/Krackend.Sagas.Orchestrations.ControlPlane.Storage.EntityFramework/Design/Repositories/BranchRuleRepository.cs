using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

/// <summary>
/// Represents BranchRuleRepository.
/// </summary>
public sealed class BranchRuleRepository : IBranchRuleRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public BranchRuleRepository(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(BranchRuleDefinition branchRuleDefinition, CancellationToken cancellationToken = default)
    {
        var stageDefinitionId = branchRuleDefinition.FromType == ElementType.Stage ? branchRuleDefinition.FromId : default;
        _dbContext.BranchRuleDefinitions.Add(branchRuleDefinition.ToEntity(stageDefinitionId));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(BranchRuleDefinition branchRuleDefinition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.BranchRuleDefinitions.FirstOrDefaultAsync(x => x.Id == branchRuleDefinition.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"BranchRuleDefinition '{branchRuleDefinition.Id}' was not found.");

        var stageDefinitionId = current.StageDefinitionId;
        if (branchRuleDefinition.FromType == ElementType.Stage)
        {
            stageDefinitionId = branchRuleDefinition.FromId;
        }

        var next = branchRuleDefinition.ToEntity(stageDefinitionId);
        _dbContext.Entry(current).CurrentValues.SetValues(next);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Delete.
    /// </summary>
    public async Task Delete(Id branchRuleDefinitionId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.BranchRuleDefinitions.FirstOrDefaultAsync(x => x.Id == branchRuleDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"BranchRuleDefinition '{branchRuleDefinitionId}' was not found.");

        _dbContext.BranchRuleDefinitions.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<IEnumerable<BranchRuleDefinition>> GetAll(Id stageDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BranchRuleDefinitions
            .AsNoTracking()
            .Where(x => x.StageDefinitionId == stageDefinitionId)
            .OrderBy(x => x.Id)
            .Select(x => x.ToDefinition())
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<BranchRuleDefinition> GetById(Id branchRuleDefinitionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BranchRuleDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == branchRuleDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"BranchRuleDefinition '{branchRuleDefinitionId}' was not found.");

        return entity.ToDefinition();
    }
}
