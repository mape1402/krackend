using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Engine;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class RuntimePendingWorkProcessorTests
{
    [Fact]
    public async Task ProcessDueWork_ReturnsWaitingTasksAttemptsAndPendingCompensations()
    {
        var now = DateTime.UtcNow;
        var task = WaitingTask(now.AddMinutes(-5));
        var attempt = WaitingAttempt(task.Id, now.AddMinutes(-4));
        var compensation = PendingCompensation(task.OrchestrationInstanceId, task.Id);
        var processor = new RuntimePendingWorkProcessor(
            new TaskRepositoryStub([task]),
            new AttemptRepositoryStub([attempt]),
            new CompensationRepositoryStub([compensation]));

        var result = await processor.ProcessDueWork(now);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.WaitingTaskCount);
        Assert.Equal(1, result.WaitingAttemptCount);
        Assert.Equal(1, result.PendingCompensationCount);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.WaitingTaskTimeout && x.Id == task.Id);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.WaitingAttemptTimeout && x.Id == attempt.Id);
        Assert.Contains(result.Items, x => x.WorkType == RuntimePendingWorkTypes.PendingCompensation && x.Id == compensation.Id);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReturnWaitingItemsNewerThanScanInstant()
    {
        var now = DateTime.UtcNow;
        var task = WaitingTask(now.AddMinutes(1));
        var attempt = WaitingAttempt(task.Id, now.AddMinutes(1));
        var processor = new RuntimePendingWorkProcessor(
            new TaskRepositoryStub([task]),
            new AttemptRepositoryStub([attempt]),
            new CompensationRepositoryStub([]));

        var result = await processor.ProcessDueWork(now);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ProcessDueWork_DoesNotReturnCompletedResponses()
    {
        var now = DateTime.UtcNow;
        var task = WaitingTask(now.AddMinutes(-5));
        task.Status = TaskExecutionStatus.Completed;
        var attempt = WaitingAttempt(task.Id, now.AddMinutes(-4));
        attempt.Status = TaskExecutionStatus.Completed;
        var processor = new RuntimePendingWorkProcessor(
            new TaskRepositoryStub([task]),
            new AttemptRepositoryStub([attempt]),
            new CompensationRepositoryStub([]));

        var result = await processor.ProcessDueWork(now);

        Assert.Empty(result.Items);
    }

    private static TaskExecution WaitingTask(DateTime waitingSinceUtc)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = Id.New(),
            StageExecutionId = Id.New(),
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.WaitingResponse,
            AwaitResponse = true,
            WaitingSinceUtc = waitingSinceUtc,
            CorrelationId = "order-1:reserve-stock"
        };

    private static TaskExecutionAttempt WaitingAttempt(Id taskExecutionId, DateTime waitingSinceUtc)
        => new()
        {
            Id = Id.New(),
            TaskExecutionId = taskExecutionId,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse,
            WaitingSinceUtc = waitingSinceUtc
        };

    private static CompensationExecution PendingCompensation(Id instanceId, Id sourceTaskExecutionId)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            SourceTaskExecutionId = sourceTaskExecutionId,
            CompensationTaskKey = "compensate:reserve-stock",
            Status = "Pending",
            StartedOnUtc = DateTime.UtcNow
        };

    private sealed class TaskRepositoryStub(IReadOnlyCollection<TaskExecution> tasks) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecution>>(tasks
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                            x.WaitingSinceUtc.HasValue &&
                            x.WaitingSinceUtc.Value <= dueBeforeUtc)
                .ToArray());
    }

    private sealed class AttemptRepositoryStub(IReadOnlyCollection<TaskExecutionAttempt> attempts) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<TaskExecutionAttempt>>(attempts
                .Where(x => x.Status == TaskExecutionStatus.WaitingResponse &&
                            x.WaitingSinceUtc.HasValue &&
                            x.WaitingSinceUtc.Value <= dueBeforeUtc)
                .ToArray());
    }

    private sealed class CompensationRepositoryStub(IReadOnlyCollection<CompensationExecution> compensations) : ICompensationExecutionRepository
    {
        public Task Create(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<CompensationExecution>>(
                compensations.Where(x => x.Status == "Pending").ToArray());
    }
}
