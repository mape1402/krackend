using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

/// <summary>
/// Represents StageRepository.
/// </summary>
public sealed class StageRepository : IStageRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public StageRepository(ControlPlaneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(StageDefinition stageDefinition, CancellationToken cancellationToken = default)
    {
        _dbContext.StageDefinitions.Add(stageDefinition.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(StageDefinition stageDefinition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.StageDefinitions.FirstOrDefaultAsync(x => x.Id == stageDefinition.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"StageDefinition '{stageDefinition.Id}' was not found.");

        var next = stageDefinition.ToEntity();
        current.OrchestrationVersionId = next.OrchestrationVersionId;
        current.Key = next.Key;
        current.Name = next.Name;
        current.Order = next.Order;
        current.Description = next.Description;
        current.ExecutionCondition = next.ExecutionCondition;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetExecutionCondition.
    /// </summary>
    public async Task SetExecutionCondition(Id stageDefinitionId, ExecutionCondition executionCondition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.StageDefinitions.FirstOrDefaultAsync(x => x.Id == stageDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"StageDefinition '{stageDefinitionId}' was not found.");

        current.ExecutionCondition = executionCondition is null
            ? null
            : new StageDefinition
            {
                Id = current.Id,
                OrchestrationVersionId = current.OrchestrationVersionId,
                Key = current.Key,
                Name = current.Name,
                Order = current.Order,
                Description = current.Description,
                ExecutionCondition = executionCondition,
                HasExecutionCondition = true,
                TaskDefinitions = new List<TaskDefinition>(),
                ParallelGroups = new List<ParallelGroupDefinition>(),
                BranchRules = new List<BranchRuleDefinition>()
            }.ToEntity().ExecutionCondition;

        // Ensure EF marks the JSON/complex property as modified for persistence.
        _dbContext.StageDefinitions.Update(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Delete.
    /// </summary>
    public async Task Delete(Id stageDefinitionId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.StageDefinitions.FirstOrDefaultAsync(x => x.Id == stageDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"StageDefinition '{stageDefinitionId}' was not found.");

        _dbContext.StageDefinitions.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<IEnumerable<StageDefinition>> GetAll(Id orchestrationVersionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StageDefinitions
            .AsNoTracking()
            .Where(x => x.OrchestrationVersionId == orchestrationVersionId)
            .OrderBy(x => x.Order)
            .Select(x => x.ToDefinition())
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<StageDefinition> GetById(Id stageDefinitionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.StageDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == stageDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"StageDefinition '{stageDefinitionId}' was not found.");

        var definition = entity.ToDefinition();
        definition.TaskDefinitions = await _dbContext.TaskDefinitions.AsNoTracking().Where(x => x.StageDefinitionId == stageDefinitionId).OrderBy(x => x.Order).Select(x => x.ToDefinition()).ToListAsync(cancellationToken);
        definition.ParallelGroups = await _dbContext.ParallelGroupDefinitions.AsNoTracking().Where(x => x.StageDefinitionId == stageDefinitionId).OrderBy(x => x.Id).Select(x => x.ToDefinition()).ToListAsync(cancellationToken);
        definition.BranchRules = await _dbContext.BranchRuleDefinitions.AsNoTracking().Where(x => x.StageDefinitionId == stageDefinitionId).OrderBy(x => x.Id).Select(x => x.ToDefinition()).ToListAsync(cancellationToken);

        return definition;
    }
}
