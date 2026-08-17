using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;

/// <summary>
/// Warms Entity Framework runtime storage metadata and common read paths.
/// </summary>
public sealed class RuntimeStorageWarmup : IRuntimeStorageWarmup
{
    private readonly RuntimeStorageDbContext _dbContext;

    public RuntimeStorageWarmup(RuntimeStorageDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task Warmup(CancellationToken cancellationToken = default)
    {
        _ = _dbContext.Model.GetEntityTypes().Count();
        await _dbContext.Database.CanConnectAsync(cancellationToken);

        await _dbContext.Artifacts.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.TriggerIntakes.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.OrchestrationInstances.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.StageExecutions.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.TaskExecutions.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.TaskExecutionAttempts.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.TaskDispatches.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await _dbContext.ExecutionTransitions.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
        await WarmupWritePipeline(cancellationToken);
    }

    private async Task WarmupWritePipeline(CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var artifactId = Id.New();
        var intakeId = Id.New();
        var instanceId = Id.New();
        var stageId = Id.New();
        var taskId = Id.New();
        var attemptId = Id.New();
        var dispatchId = Id.New();
        var suffix = Guid.NewGuid().ToString("N");

        _dbContext.Artifacts.Add(new RuntimeArtifactEntity
        {
            Id = artifactId,
            EnvironmentKey = "__warmup",
            OrchestrationDefinitionKey = $"__warmup-{suffix}",
            ArtifactType = "Warmup",
            SourceOrchestrationVersionId = Id.New(),
            Version = "0.0.0",
            ArtifactChecksum = suffix,
            ArtifactPayloadJson = "{}",
            IsActive = false,
            LoadedToCache = true,
            DeployedOnUtc = now
        });

        _dbContext.TriggerIntakes.Add(new TriggerIntakeEntity
        {
            Id = intakeId,
            TriggerType = TriggerType.Event,
            TriggerKey = "__warmup",
            EnvironmentKey = "__warmup",
            CorrelationId = suffix,
            IdempotencyKey = suffix,
            SourceMessageId = suffix,
            SourceRequestId = suffix,
            RawPayloadJson = "{}",
            NormalizedPayloadJson = "{}",
            Status = TriggerIntakeStatus.PersistedPrimary,
            PersistenceLevel = "Warmup",
            BufferLocation = "Warmup",
            ResolvedArtifactId = artifactId,
            ReceivedOnUtc = now
        });

        _dbContext.OrchestrationInstances.Add(new OrchestrationInstanceEntity
        {
            Id = instanceId,
            EnvironmentKey = "__warmup",
            OrchestrationDefinitionKey = "__warmup",
            RuntimeOrchestrationArtifactId = artifactId,
            TriggerIntakeId = intakeId,
            CorrelationId = suffix,
            ExecutionKey = $"__warmup::{suffix}",
            Status = OrchestrationInstanceStatus.Created,
            StartedOnUtc = now,
            LastUpdatedOnUtc = now,
            SnapshotPayloadJson = "{}",
            MetadataJson = "{}"
        });

        _dbContext.StageExecutions.Add(new StageExecutionEntity
        {
            Id = stageId,
            OrchestrationInstanceId = instanceId,
            StageKey = "__warmup",
            Order = 1,
            Status = StageExecutionStatus.Running,
            ExecutionConditionResult = true,
            StartedOnUtc = now,
            ParallelGroupCount = 0,
            MetadataJson = "{}"
        });

        _dbContext.TaskExecutions.Add(new TaskExecutionEntity
        {
            Id = taskId,
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageId,
            TaskKey = "__warmup",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.Running,
            ExecutionConditionResult = true,
            OnErrorPolicy = OnErrorPolicy.Stop,
            AwaitResponse = true,
            StartedOnUtc = now,
            LastAttemptNumber = 1,
            CorrelationId = suffix,
            MetadataJson = "{}"
        });

        _dbContext.TaskExecutionAttempts.Add(new TaskExecutionAttemptEntity
        {
            Id = attemptId,
            TaskExecutionId = taskId,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running,
            StartedOnUtc = now,
            RequestPayloadJson = "{}",
            MetadataJson = "{}"
        });

        _dbContext.TaskDispatches.Add(new TaskDispatchEntity
        {
            Id = dispatchId,
            TaskExecutionAttemptId = attemptId,
            DispatchType = "Messaging",
            Destination = "__warmup",
            RequestPayloadJson = "{}",
            DispatchStatus = "Pending",
            CommandId = suffix,
            CorrelationId = suffix,
            MetadataJson = "{}"
        });

        _dbContext.ExecutionTransitions.Add(new ExecutionTransitionEntity
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageId,
            TaskExecutionId = taskId,
            TaskExecutionAttemptId = attemptId,
            TransitionType = "Warmup",
            FromStatus = "Created",
            ToStatus = "Running",
            OccurredOnUtc = now,
            Message = "Runtime storage warmup.",
            PayloadJson = "{}",
            ProducedBy = nameof(RuntimeStorageWarmup)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var instance = await _dbContext.OrchestrationInstances.FirstAsync(x => x.Id == instanceId, cancellationToken);
        instance.Status = OrchestrationInstanceStatus.Running;
        instance.CurrentStageKey = "__warmup";
        instance.CurrentTaskKey = "__warmup";
        instance.LastUpdatedOnUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await transaction.RollbackAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }
}
