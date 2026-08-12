using Microsoft.EntityFrameworkCore;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Repositories;

public sealed class TriggerIntakeRepository : ITriggerIntakeRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public TriggerIntakeRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(TriggerIntake intake, CancellationToken cancellationToken = default)
    {
        _dbContext.TriggerIntakes.Add(RuntimeStorageMapper.ToEntity(intake));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(TriggerIntake intake, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TriggerIntakes.FirstAsync(x => x.Id == intake.Id, cancellationToken);
        _dbContext.Entry(entity).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(intake));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TriggerIntake> GetById(Id intakeId, CancellationToken cancellationToken = default)
        => RuntimeStorageMapper.ToDomain(await _dbContext.TriggerIntakes.AsNoTracking().FirstAsync(x => x.Id == intakeId, cancellationToken));

    public async Task<TriggerIntake> GetByIdempotencyKey(
        string environmentKey,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TriggerIntakes.AsNoTracking().FirstOrDefaultAsync(
            x => x.EnvironmentKey == environmentKey && x.IdempotencyKey == idempotencyKey,
            cancellationToken);

        return entity is null ? null : RuntimeStorageMapper.ToDomain(entity);
    }
}

public sealed class TriggerIntakeAttemptRepository : ITriggerIntakeAttemptRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public TriggerIntakeAttemptRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(TriggerIntakeAttempt attempt, CancellationToken cancellationToken = default)
    {
        _dbContext.TriggerIntakeAttempts.Add(RuntimeStorageMapper.ToEntity(attempt));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TriggerIntakeAttempt>> GetByIntakeId(Id intakeId, CancellationToken cancellationToken = default)
        => await _dbContext.TriggerIntakeAttempts.AsNoTracking()
            .Where(x => x.TriggerIntakeId == intakeId)
            .OrderBy(x => x.AttemptNumber)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
}

public sealed class OrchestrationInstanceRepository : IOrchestrationInstanceRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public OrchestrationInstanceRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        _dbContext.OrchestrationInstances.Add(RuntimeStorageMapper.ToEntity(instance));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OrchestrationInstances.FirstAsync(x => x.Id == instance.Id, cancellationToken);
        _dbContext.Entry(entity).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(instance));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
        => RuntimeStorageMapper.ToDomain(await _dbContext.OrchestrationInstances.AsNoTracking().FirstAsync(x => x.Id == instanceId, cancellationToken));

    public async Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(
        string environmentKey,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 1000);
        return await _dbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .OrderByDescending(x => x.LastUpdatedOnUtc)
            .Take(limit)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<RuntimeInstanceSummary> GetSummary(
        string environmentKey,
        DateTime recentSinceUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OrchestrationInstances.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey);

        var active = await query.CountAsync(
            x => x.Status == OrchestrationInstanceStatus.Running ||
                 x.Status == OrchestrationInstanceStatus.Waiting,
            cancellationToken);

        var waiting = await query.CountAsync(
            x => x.Status == OrchestrationInstanceStatus.Waiting,
            cancellationToken);

        var completedRecent = await query.CountAsync(
            x => x.Status == OrchestrationInstanceStatus.Completed &&
                 x.LastUpdatedOnUtc >= recentSinceUtc,
            cancellationToken);

        var failedRecent = await query.CountAsync(
            x => x.Status == OrchestrationInstanceStatus.Failed &&
                 x.LastUpdatedOnUtc >= recentSinceUtc,
            cancellationToken);

        return new RuntimeInstanceSummary(active, waiting, completedRecent, failedRecent, recentSinceUtc);
    }
}

public sealed class StageExecutionRepository : IStageExecutionRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public StageExecutionRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(StageExecution stageExecution, CancellationToken cancellationToken = default)
    {
        _dbContext.StageExecutions.Add(RuntimeStorageMapper.ToEntity(stageExecution));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(StageExecution stageExecution, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.StageExecutions.FirstAsync(x => x.Id == stageExecution.Id, cancellationToken);
        _dbContext.Entry(entity).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(stageExecution));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StageExecution> GetById(Id stageExecutionId, CancellationToken cancellationToken = default)
        => RuntimeStorageMapper.ToDomain(await _dbContext.StageExecutions.AsNoTracking().FirstAsync(x => x.Id == stageExecutionId, cancellationToken));

    public async Task<IReadOnlyCollection<StageExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await _dbContext.StageExecutions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.Order)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
}

public sealed class TaskExecutionRepository : ITaskExecutionRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public TaskExecutionRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default)
    {
        _dbContext.TaskExecutions.Add(RuntimeStorageMapper.ToEntity(taskExecution));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskExecutions.FirstAsync(x => x.Id == taskExecution.Id, cancellationToken);
        _dbContext.Entry(entity).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(taskExecution));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
        => RuntimeStorageMapper.ToDomain(await _dbContext.TaskExecutions.AsNoTracking().FirstAsync(x => x.Id == taskExecutionId, cancellationToken));

    public async Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskExecutions.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);
        return entity is null ? null : RuntimeStorageMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await _dbContext.TaskExecutions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
}

public sealed class TaskExecutionAttemptRepository : ITaskExecutionAttemptRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public TaskExecutionAttemptRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        _dbContext.TaskExecutionAttempts.Add(RuntimeStorageMapper.ToEntity(attempt));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskExecutionAttempts.FirstAsync(x => x.Id == attempt.Id, cancellationToken);
        _dbContext.Entry(entity).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(attempt));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
        => RuntimeStorageMapper.ToDomain(await _dbContext.TaskExecutionAttempts.AsNoTracking().FirstAsync(x => x.Id == attemptId, cancellationToken));

    public async Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
        => await _dbContext.TaskExecutionAttempts.AsNoTracking()
            .Where(x => x.TaskExecutionId == taskExecutionId)
            .OrderBy(x => x.AttemptNumber)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
}

public sealed class TaskDispatchRepository : ITaskDispatchRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public TaskDispatchRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        _dbContext.TaskDispatches.Add(RuntimeStorageMapper.ToEntity(dispatch));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskDispatches.FirstAsync(x => x.Id == dispatch.Id, cancellationToken);
        _dbContext.Entry(entity).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(dispatch));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
        => RuntimeStorageMapper.ToDomain(await _dbContext.TaskDispatches.AsNoTracking().FirstAsync(x => x.Id == dispatchId, cancellationToken));

    public async Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskDispatches.AsNoTracking().FirstOrDefaultAsync(x => x.CommandId == commandId, cancellationToken);
        return entity is null ? null : RuntimeStorageMapper.ToDomain(entity);
    }

    public async Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TaskDispatches.AsNoTracking().FirstOrDefaultAsync(x => x.TaskExecutionAttemptId == taskExecutionAttemptId, cancellationToken);
        return entity is null ? null : RuntimeStorageMapper.ToDomain(entity);
    }
}

public sealed class ExecutionTransitionRepository : IExecutionTransitionRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public ExecutionTransitionRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default)
    {
        _dbContext.ExecutionTransitions.Add(RuntimeStorageMapper.ToEntity(transition));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await _dbContext.ExecutionTransitions.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .OrderBy(x => x.OccurredOnUtc)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(
        string environmentKey,
        int take = 250,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 1000);
        return await _dbContext.ExecutionTransitions.AsNoTracking()
            .Join(
                _dbContext.OrchestrationInstances.AsNoTracking().Where(x => x.EnvironmentKey == environmentKey),
                transition => transition.OrchestrationInstanceId,
                instance => instance.Id,
                (transition, _) => transition)
            .OrderByDescending(x => x.OccurredOnUtc)
            .Take(limit)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(
        string environmentKey,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ExecutionTransitions.AsNoTracking()
            .Join(
                _dbContext.OrchestrationInstances.AsNoTracking().Where(x => x.EnvironmentKey == environmentKey),
                transition => transition.OrchestrationInstanceId,
                instance => instance.Id,
                (transition, _) => transition)
            .Where(x => x.OccurredOnUtc >= sinceUtc &&
                (x.TransitionType == "InstanceStarted" ||
                 x.TransitionType == "InstanceCompleted" ||
                 x.TransitionType == "InstanceFailed"))
            .GroupBy(x => new
            {
                x.OccurredOnUtc.Year,
                x.OccurredOnUtc.Month,
                x.OccurredOnUtc.Day,
                x.OccurredOnUtc.Hour,
                x.OccurredOnUtc.Minute
            })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .ThenBy(x => x.Key.Day)
            .ThenBy(x => x.Key.Hour)
            .ThenBy(x => x.Key.Minute)
            .Select(x => new RuntimeTrafficPoint(
                new DateTime(x.Key.Year, x.Key.Month, x.Key.Day, x.Key.Hour, x.Key.Minute, 0, DateTimeKind.Utc),
                x.Count(item => item.TransitionType == "InstanceStarted"),
                x.Count(item => item.TransitionType == "InstanceCompleted"),
                x.Count(item => item.TransitionType == "InstanceFailed")))
            .ToArrayAsync(cancellationToken);
    }
}

public sealed class InstanceVariableRepository : IInstanceVariableRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public InstanceVariableRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Upsert(InstanceVariable variable, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.InstanceVariables.FirstOrDefaultAsync(
            x => x.OrchestrationInstanceId == variable.OrchestrationInstanceId && x.Key == variable.Key,
            cancellationToken);

        if (existing is null)
        {
            _dbContext.InstanceVariables.Add(RuntimeStorageMapper.ToEntity(variable));
        }
        else
        {
            variable.Id = existing.Id;
            _dbContext.Entry(existing).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(variable));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<InstanceVariable>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
        => await _dbContext.InstanceVariables.AsNoTracking()
            .Where(x => x.OrchestrationInstanceId == instanceId)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
}

public sealed class EnvironmentVariableRepository : IEnvironmentVariableRepository
{
    private readonly RuntimeStorageDbContext _dbContext;

    public EnvironmentVariableRepository(RuntimeStorageDbContext dbContext) => _dbContext = dbContext;

    public async Task Upsert(EnvironmentVariableValue variable, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.EnvironmentVariables.FirstOrDefaultAsync(
            x => x.EnvironmentKey == variable.EnvironmentKey && x.VariableKey == variable.VariableKey,
            cancellationToken);

        if (existing is null)
        {
            _dbContext.EnvironmentVariables.Add(RuntimeStorageMapper.ToEntity(variable));
        }
        else
        {
            variable.Id = existing.Id;
            _dbContext.Entry(existing).CurrentValues.SetValues(RuntimeStorageMapper.ToEntity(variable));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EnvironmentVariableValue>> GetByEnvironmentKey(string environmentKey, CancellationToken cancellationToken = default)
        => await _dbContext.EnvironmentVariables.AsNoTracking()
            .Where(x => x.EnvironmentKey == environmentKey)
            .Select(x => RuntimeStorageMapper.ToDomain(x))
            .ToArrayAsync(cancellationToken);
}
