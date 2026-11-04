using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Mappings;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;

/// <summary>
/// Represents TaskRepository.
/// </summary>
public sealed class TaskRepository : ITaskRepository
{
    private readonly ControlPlaneDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dbContext">The dbContext value.</param>
    public TaskRepository(ControlPlaneDbContext dbContext)
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
    public Task SetExecutionCondition(Id taskDefinitionId, ExecutionCondition executionCondition, CancellationToken cancellationToken = default)
        => SetExecutionConditionCore(taskDefinitionId, executionCondition, null, cancellationToken);

    /// <inheritdoc />
    public Task SetExecutionConditionIfUnchanged(Id id, ExecutionCondition value, string expectedRevision, CancellationToken cancellationToken = default)
        => SetExecutionConditionCore(id, value, expectedRevision ?? string.Empty, cancellationToken);

    private async Task SetExecutionConditionCore(Id taskDefinitionId, ExecutionCondition executionCondition, string expectedRevision, CancellationToken cancellationToken)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        if (expectedRevision is not null)
            DesignerConcurrencyGuard.EnsureCondition(expectedRevision, current.ToDefinition().ExecutionCondition);

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

        await DesignerConcurrencyGuard.SaveAsync(_dbContext, expectedRevision is not null, cancellationToken);
    }

    /// <summary>
    /// Executes SetTransformation.
    /// </summary>
    public Task SetTransformation(Id taskDefinitionId, TransformationDefinition transformation, CancellationToken cancellationToken = default)
        => SetTransformationCore(taskDefinitionId, transformation, null, cancellationToken);

    /// <inheritdoc />
    public Task SetTransformationIfUnchanged(Id id, TransformationDefinition value, string expectedRevision, CancellationToken cancellationToken = default)
        => SetTransformationCore(id, value, expectedRevision ?? string.Empty, cancellationToken);

    private async Task SetTransformationCore(Id taskDefinitionId, TransformationDefinition transformation, string expectedRevision, CancellationToken cancellationToken)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        if (expectedRevision is not null)
            DesignerConcurrencyGuard.EnsureTransformation(expectedRevision, current.ToDefinition().Transformation);

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

        await DesignerConcurrencyGuard.SaveAsync(_dbContext, expectedRevision is not null, cancellationToken);
    }

    /// <summary>
    /// Executes SetCompensationExecutionCondition.
    /// </summary>
    public Task SetCompensationExecutionCondition(Id taskDefinitionId, ExecutionCondition executionCondition, CancellationToken cancellationToken = default)
        => SetCompensationExecutionConditionCore(taskDefinitionId, executionCondition, null, cancellationToken);

    /// <inheritdoc />
    public Task SetCompensationExecutionConditionIfUnchanged(Id id, ExecutionCondition value, string expectedRevision, CancellationToken cancellationToken = default)
        => SetCompensationExecutionConditionCore(id, value, expectedRevision ?? string.Empty, cancellationToken);

    private async Task SetCompensationExecutionConditionCore(Id taskDefinitionId, ExecutionCondition executionCondition, string expectedRevision, CancellationToken cancellationToken)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        var definition = current.ToDefinition();
        if (expectedRevision is not null)
            DesignerConcurrencyGuard.EnsureCondition(expectedRevision, definition.CompensationDefinition?.ExecutionCondition);
        definition.CompensationDefinition = EnsureCompensation(definition.CompensationDefinition);
        definition.CompensationDefinition.ExecutionCondition = executionCondition;
        definition.CompensationDefinition.HasExecutionCondition = executionCondition is not null;

        current.CompensationDefinition = definition.ToEntity().CompensationDefinition;
        await DesignerConcurrencyGuard.SaveAsync(_dbContext, expectedRevision is not null, cancellationToken);
    }

    /// <summary>
    /// Executes SetCompensationTransformation.
    /// </summary>
    public Task SetCompensationTransformation(Id taskDefinitionId, TransformationDefinition transformation, CancellationToken cancellationToken = default)
        => SetCompensationTransformationCore(taskDefinitionId, transformation, null, cancellationToken);

    /// <inheritdoc />
    public Task SetCompensationTransformationIfUnchanged(Id id, TransformationDefinition value, string expectedRevision, CancellationToken cancellationToken = default)
        => SetCompensationTransformationCore(id, value, expectedRevision ?? string.Empty, cancellationToken);

    private async Task SetCompensationTransformationCore(Id taskDefinitionId, TransformationDefinition transformation, string expectedRevision, CancellationToken cancellationToken)
    {
        var current = await _dbContext.TaskDefinitions.FirstOrDefaultAsync(x => x.Id == taskDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"TaskDefinition '{taskDefinitionId}' was not found.");

        var definition = current.ToDefinition();
        if (expectedRevision is not null)
            DesignerConcurrencyGuard.EnsureTransformation(expectedRevision, definition.CompensationDefinition?.Transformation);
        definition.CompensationDefinition = EnsureCompensation(definition.CompensationDefinition);
        definition.CompensationDefinition.Transformation = transformation;
        definition.CompensationDefinition.HasTransformation = transformation is not null;

        current.CompensationDefinition = definition.ToEntity().CompensationDefinition;
        await DesignerConcurrencyGuard.SaveAsync(_dbContext, expectedRevision is not null, cancellationToken);
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

    private static CompensationDefinition EnsureCompensation(CompensationDefinition compensation)
        => compensation ?? new CompensationDefinition
        {
            CompensationTaskKind = TaskKind.Messaging,
            Configuration = new MessagingTaskConfiguration { Topic = string.Empty },
            DispatchType = TaskDispatchType.FireAndForget,
            OnErrorPolicy = OnErrorPolicy.Stop,
        };
}
