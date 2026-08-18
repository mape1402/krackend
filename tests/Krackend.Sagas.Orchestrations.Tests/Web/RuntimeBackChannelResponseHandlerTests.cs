using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Consuming;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Metadata;
using Krackend.Sagas.Orchestrations.Web;

namespace Krackend.Sagas.Orchestrations.Tests.Web;

public sealed class RuntimeBackChannelResponseHandlerTests
{
    [Fact]
    public async Task Handle_Should_Schedule_Response_Ingress_With_Trace_Metadata()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var store = BackChannelStore.Create();
        var handler = new RuntimeBackChannelResponseHandler(scheduler, store.Dispatches, store.Tasks, store.Attempts);

        await handler.Handle(CreateContext(store));

        var envelope = Assert.IsType<RuntimeIngressEnvelope>(scheduler.ProcessIngressEnvelope);
        Assert.Equal(RuntimeIngressKind.TaskResponse, envelope.Kind);
        Assert.Equal("local", envelope.EnvironmentKey);
        Assert.Equal("order.fulfillment", envelope.OrchestrationName);
        Assert.Equal("2.1.0", envelope.OrchestrationVersion);
        Assert.Equal(store.InstanceId.ToString(), envelope.OrchestrationInstanceId);
        Assert.Equal(store.Dispatch.Id.ToString(), envelope.DispatchId);
        Assert.Equal(store.Task.Id.ToString(), envelope.TaskExecutionId);
        Assert.Equal("corr-1", envelope.CorrelationId);
        Assert.Equal("saga-1", envelope.SagaId);
        Assert.Equal(4, envelope.Attempt);
        Assert.Equal(RuntimeTransportKind.Message, envelope.Source.Kind);
        Assert.Equal("orders.backchannel", envelope.Source.Address);
        Assert.Equal("2.1.0", envelope.Source.Version);
        Assert.Equal(store.Dispatch.Id.ToString(), envelope.Source.MessageId);
        Assert.Equal("paid", envelope.Payload["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Handle_Should_Reject_Response_Without_Dispatch_Id()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var store = BackChannelStore.Create();
        var handler = new RuntimeBackChannelResponseHandler(scheduler, store.Dispatches, store.Tasks, store.Attempts);
        var context = CreateContext(store, dispatchId: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(context));

        Assert.Equal("Back-channel response metadata must include dispatch id.", ex.Message);
    }

    [Fact]
    public async Task Handle_Should_Reject_Crossed_Instance_Trace()
    {
        var scheduler = new CapturingDurableWorkScheduler();
        var store = BackChannelStore.Create();
        var handler = new RuntimeBackChannelResponseHandler(scheduler, store.Dispatches, store.Tasks, store.Attempts);
        var context = CreateContext(store, instanceId: Id.New().ToString());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(context));

        Assert.Contains("does not belong to orchestration instance", ex.Message);
    }

    private static MessageConsumeContext CreateContext(
        BackChannelStore store,
        string dispatchId = null,
        string instanceId = null,
        string taskExecutionId = null,
        string correlationId = "corr-1")
        => new()
        {
            Topic = "orders.backchannel",
            Version = "2.1.0",
            CreatedOnUtc = new DateTimeOffset(2026, 8, 14, 1, 2, 3, TimeSpan.Zero),
            Message = JsonNode.Parse("""{"status":"paid"}"""),
            Metadata = new OrchestratorMessageMetadata
            {
                SagaId = "saga-1",
                OrchestrationId = "orchestration-1",
                OrchestrationKey = "order.fulfillment",
                OrchestrationVersion = "2.1.0",
                OrchestrationInstanceId = instanceId ?? store.InstanceId.ToString(),
                TaskExecutionId = taskExecutionId ?? store.Task.Id.ToString(),
                DispatchId = dispatchId ?? store.Dispatch.Id.ToString(),
                CorrelationId = correlationId,
                ResponseTopic = "orders.backchannel",
                ResponseVersion = "2.1.0",
                Environment = "local",
                CurrentState = new OrchestrationRuntimeState
                {
                    Status = "Waiting",
                    CurrentStageKey = "payment",
                    CurrentTaskKey = "charge-payment",
                    Attempt = 4,
                    StartedOnUtc = new DateTime(2026, 8, 14, 1, 2, 0, DateTimeKind.Utc),
                    UpdatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 2, DateTimeKind.Utc)
                }
            }
        };

    private sealed class BackChannelStore
    {
        private BackChannelStore()
        {
            InstanceId = Id.New();
            Task = new TaskExecution
            {
                Id = Id.New(),
                OrchestrationInstanceId = InstanceId,
                StageExecutionId = Id.New(),
                TaskKey = "charge-payment",
                TaskKind = TaskKind.Messaging,
                Status = TaskExecutionStatus.WaitingResponse,
                CorrelationId = "corr-1",
                Metadata = new Dictionary<string, JsonNode>()
            };
            Attempt = new TaskExecutionAttempt
            {
                Id = Id.New(),
                TaskExecutionId = Task.Id,
                AttemptNumber = 4,
                Status = TaskExecutionStatus.WaitingResponse,
                DispatchId = Id.New(),
                Metadata = new Dictionary<string, JsonNode>()
            };
            Dispatch = new TaskDispatch
            {
                Id = Attempt.DispatchId.Value,
                TaskExecutionAttemptId = Attempt.Id,
                DispatchType = "Messaging",
                Destination = "orders.payment",
                DispatchStatus = "Published",
                CorrelationId = "corr-1",
                Metadata = new Dictionary<string, JsonNode>()
            };
            Tasks = new TaskRepository(Task);
            Attempts = new AttemptRepository(Attempt);
            Dispatches = new DispatchRepository(Dispatch);
        }

        public Id InstanceId { get; }

        public TaskExecution Task { get; }

        public TaskExecutionAttempt Attempt { get; }

        public TaskDispatch Dispatch { get; }

        public ITaskExecutionRepository Tasks { get; }

        public ITaskExecutionAttemptRepository Attempts { get; }

        public ITaskDispatchRepository Dispatches { get; }

        public static BackChannelStore Create() => new();
    }

    private sealed class TaskRepository(TaskExecution task) : ITaskExecutionRepository
    {
        public Task Create(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Update(TaskExecution taskExecution, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskExecution> GetById(Id taskExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(taskExecutionId == task.Id ? task : null!);

        public Task<TaskExecution> GetByCorrelationId(string correlationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskExecution> GetByStageAndKey(Id stageExecutionId, string taskKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecution>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class AttemptRepository(TaskExecutionAttempt attempt) : ITaskExecutionAttemptRepository
    {
        public Task Create(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Update(TaskExecutionAttempt attempt, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskExecutionAttempt> GetById(Id attemptId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskExecutionAttempt> GetByDispatchId(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(attempt.DispatchId == dispatchId ? attempt : null!);

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetByTaskExecutionId(Id taskExecutionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskExecutionAttempt>> GetWaitingResponseOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class DispatchRepository(TaskDispatch dispatch) : ITaskDispatchRepository
    {
        public Task Create(TaskDispatch dispatch, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Update(TaskDispatch dispatch, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task MarkSent(Id dispatchId, string status, DateTime sentOnUtc, string externalReference = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task MarkFailed(Id dispatchId, string failureReason, string externalReference = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(dispatchId == dispatch.Id ? dispatch : null!);

        public Task<TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyCollection<TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class CapturingDurableWorkScheduler : IRuntimeDurableWorkScheduler
    {
        public object ProcessIngressEnvelope { get; private set; } = null!;

        public ValueTask<Guid> ScheduleProcessIngress(RuntimeIngressEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ProcessIngressEnvelope = envelope;
            return ValueTask.FromResult(Guid.NewGuid());
        }

        public ValueTask<Guid> ScheduleDispatchTask(RuntimeDispatchEnvelope envelope, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask<Guid> ScheduleReconcile(RuntimeReconcileRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
