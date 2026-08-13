using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Core;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Mappings;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;

/// <summary>
/// Represents TaskRepository.
/// </summary>
public sealed class TaskRepository : ITaskRepository
{
    private readonly DesignStorageDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public TaskRepository(DesignStorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes Create.
    /// </summary>
    public async Task Create(TaskDefinition taskDefinition, CancellationToken cancellationToken = default)
    {
        _dbContext.TaskDefinitions.Add(taskDefinition.ToEntity());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Update.
    /// </summary>
    public async Task Update(TaskDefinition taskDefinition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinition.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinition.Id}' was not found.");

        var next = taskDefinition.ToEntity();
        current.StageDefinitionId = next.StageDefinitionId;
        current.Key = next.Key;
        current.Name = next.Name;
        current.Order = next.Order;
        current.Kind = next.Kind;
        current.ExecutionMode = next.ExecutionMode;
        current.ParallelGroupId = next.ParallelGroupId;
        current.OnErrorPolicy = next.OnErrorPolicy;
        current.DispatchType = next.DispatchType;
        current.IsEnabled = next.IsEnabled;
        current.Notes = next.Notes;
        current.ExecutionCondition = next.ExecutionCondition;
        current.Transformation = next.Transformation;
        current.Configuration = next.Configuration;
        current.RetryPolicy = next.RetryPolicy;
        current.TimeoutPolicy = next.TimeoutPolicy;
        current.CompensationDefinition = next.CompensationDefinition;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes Delete.
    /// </summary>
    public async Task Delete(Id taskDefinitionId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        _dbContext.TaskDefinitions.Remove(current);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetIsEnabled.
    /// </summary>
    public async Task SetIsEnabled(Id taskDefinitionId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        current.IsEnabled = isEnabled;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetExecutionCondition.
    /// </summary>
    public async Task SetExecutionCondition(Id taskDefinitionId, ExecutionCondition executionCondition, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        current.ExecutionCondition = executionCondition is null
            ? null
            : new TaskDefinition
        {
            Id = current.Id,
            StageDefinitionId = current.StageDefinitionId,
            Key = current.Key,
            Name = current.Name,
            Order = current.Order,
            Notes = current.Notes,
            Kind = current.Kind,
            ExecutionMode = current.ExecutionMode,
            ParallelGroupId = current.ParallelGroupId,
            OnErrorPolicy = current.OnErrorPolicy,
            DispatchType = current.DispatchType,
            IsEnabled = current.IsEnabled,
            ExecutionCondition = executionCondition,
            HasExecutionCondition = true,
            Transformation = null,
            Configuration = new HumanApprovalTaskConfiguration(),
            RetryPolicy = null,
            TimeoutPolicy = null,
            CompensationDefinition = null
        }.ToEntity().ExecutionCondition;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes SetTransformation.
    /// </summary>
    public async Task SetTransformation(Id taskDefinitionId, TransformationDefinition transformation, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        current.Transformation = transformation is null
            ? null
            : new TaskDefinition
            {
                Id = current.Id,
                StageDefinitionId = current.StageDefinitionId,
                Key = current.Key,
                Name = current.Name,
                Order = current.Order,
                Notes = current.Notes,
                Kind = current.Kind,
                ExecutionMode = current.ExecutionMode,
                ParallelGroupId = current.ParallelGroupId,
                OnErrorPolicy = current.OnErrorPolicy,
                DispatchType = current.DispatchType,
                IsEnabled = current.IsEnabled,
                ExecutionCondition = null,
                Transformation = transformation,
                HasTransformation = true,
                Configuration = new HumanApprovalTaskConfiguration(),
                RetryPolicy = null,
                TimeoutPolicy = null,
                CompensationDefinition = null
            }.ToEntity().Transformation;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetAll.
    /// </summary>
    public async Task<IEnumerable<TaskDefinition>> GetAll(Id stageDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TaskDefinitions
            .AsNoTracking()
            .Where(x => x.StageDefinitionId == stageDefinitionId)
            .OrderBy(x => x.Order)
            .Select(x => x.ToDefinition())
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes GetById.
    /// </summary>
    public async Task<TaskDefinition> GetById(Id taskDefinitionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        return entity.ToDefinition();
    }
}
