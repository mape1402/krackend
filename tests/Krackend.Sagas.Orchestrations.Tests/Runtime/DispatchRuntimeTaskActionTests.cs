using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Dispatch;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;
using Krackend.Sagas.Orchestrations.Engine;
using Krackend.Sagas.Orchestrations.Engine.DurableWork;
using Microsoft.Extensions.DependencyInjection;
using Mule;

namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

public sealed class DispatchRuntimeTaskActionTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Publish_Message_Dispatch()
    {
        var dispatcher = new CapturingMessagingCommandDispatcher();
        var dispatchRepository = new CapturingTaskDispatchRepository();
        var action = CreateAction(dispatcher, dispatchRepository);
        var envelope = CreateEnvelope();
        dispatchRepository.Dispatch.Id = new Id(Ulid.Parse(envelope.DispatchId));

        await action.ExecuteAsync(CreateContext(envelope), CancellationToken.None);

        Assert.Equal("orders.payment", dispatcher.Command.Destination);
        Assert.Equal("1.0.0", dispatcher.Command.MessageVersion);
        Assert.Equal("local", dispatcher.Command.EnvironmentKey);
        Assert.Equal(envelope.DispatchId, dispatcher.Command.DispatchId);
        Assert.Equal("Published", dispatchRepository.Dispatch.DispatchStatus);
        Assert.NotNull(dispatchRepository.Dispatch.SentOnUtc);
        Assert.Equal("message-1", dispatchRepository.Dispatch.Metadata["externalReference"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_Reject_Unsupported_Transport()
    {
        var dispatcher = new CapturingMessagingCommandDispatcher();
        var action = CreateAction(dispatcher, new CapturingTaskDispatchRepository());
        var envelope = CreateEnvelope();
        envelope.Destination.Kind = RuntimeTransportKind.Http;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => action.ExecuteAsync(CreateContext(envelope), CancellationToken.None).AsTask());

        Assert.Equal("Runtime dispatch transport 'Http' is not supported yet.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompensationMetadataExists_CompletesCompensationAfterPublish()
    {
        var dispatcher = new CapturingMessagingCommandDispatcher();
        var dispatchRepository = new CapturingTaskDispatchRepository();
        var compensationRepository = new CapturingCompensationRepository();
        var instanceRepository = new CapturingInstanceRepository();
        var transitionRepository = new CapturingTransitionRepository();
        var action = new DispatchRuntimeTaskAction(
            dispatcher,
            dispatchRepository,
            compensationRepository,
            instanceRepository,
            transitionRepository,
            new NoopRuntimeReactiveEventPublisher());
        var envelope = CreateEnvelope();
        dispatchRepository.Dispatch.Id = new Id(Ulid.Parse(envelope.DispatchId));
        envelope.OrchestrationInstanceId = instanceRepository.Instance.Id.ToString();
        envelope.Metadata["compensationExecutionId"] = compensationRepository.Compensation.Id.ToString();

        await action.ExecuteAsync(CreateContext(envelope), CancellationToken.None);

        Assert.Equal("Completed", compensationRepository.Compensation.Status);
        Assert.NotNull(compensationRepository.Compensation.CompletedOnUtc);
        Assert.Equal("Published", compensationRepository.Compensation.Metadata["dispatchStatus"]!.GetValue<string>());
        Assert.Equal(OrchestrationInstanceStatus.Compensated, instanceRepository.Instance.Status);
        Assert.Contains(transitionRepository.Transitions, x => x.TransitionType == "CompensationCompleted");
        Assert.Contains(transitionRepository.Transitions, x => x.TransitionType == "InstanceCompensated");
    }

    private static DispatchRuntimeTaskAction CreateAction(
        IMessagingCommandDispatcher dispatcher,
        ITaskDispatchRepository dispatchRepository)
        => new(
            dispatcher,
            dispatchRepository,
            new CapturingCompensationRepository(),
            new CapturingInstanceRepository(),
            new CapturingTransitionRepository(),
            new NoopRuntimeReactiveEventPublisher());

    private static RuntimeDispatchEnvelope CreateEnvelope()
        => new()
        {
            DispatchId = Id.New().ToString(),
            OrchestrationInstanceId = "instance-1",
            ExecutionKey = "cmd-1",
            CorrelationId = "corr-1",
            SagaId = "saga-1",
            EnvironmentKey = "local",
            OrchestrationName = "order.fulfillment",
            OrchestrationVersion = "2.0.0",
            StageKey = "payment",
            TaskKey = "charge-payment",
            TaskExecutionId = "task-exec-1",
            Attempt = 1,
            Payload = JsonNode.Parse("""{"amount":10}""")!,
            CreatedOnUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
            Destination = new RuntimeTransportDescriptor
            {
                Kind = RuntimeTransportKind.Message,
                Address = "orders.payment",
                Version = "1.0.0"
            }
        };

    private static MuleActionContext<RuntimeDispatchEnvelope> CreateContext(RuntimeDispatchEnvelope envelope)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var action = new DurableAction
        {
            Id = Guid.NewGuid(),
            Key = RuntimeDurableWorkActionKeys.DispatchTask,
            Payload = "{}",
            PayloadType = typeof(RuntimeDispatchEnvelope).AssemblyQualifiedName,
            Status = DurableActionStatus.Locked,
            CreatedOnUtc = DateTimeOffset.UtcNow
        };

        return new MuleActionContext<RuntimeDispatchEnvelope>(action, services, envelope);
    }

    private sealed class CapturingMessagingCommandDispatcher : IMessagingCommandDispatcher
    {
        public MessagingDispatchCommand Command { get; private set; } = null!;

        public Task<MessagingDispatchResult> Dispatch(MessagingDispatchCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(new MessagingDispatchResult
            {
                Succeeded = true,
                Status = "Published",
                ExternalReference = "message-1"
            });
        }
    }

    private sealed class CapturingTaskDispatchRepository : ITaskDispatchRepository
    {
        public Abstractions.Runtime.TaskDispatch Dispatch { get; } = new()
        {
            Id = Id.New(),
            TaskExecutionAttemptId = Id.New(),
            DispatchType = "Messaging",
            Destination = "orders.payment",
            DispatchStatus = "Scheduled",
            Metadata = new Dictionary<string, JsonNode>()
        };

        public Task Create(Abstractions.Runtime.TaskDispatch dispatch, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(Abstractions.Runtime.TaskDispatch dispatch, CancellationToken cancellationToken = default)
        {
            Dispatch.DispatchStatus = dispatch.DispatchStatus;
            Dispatch.ScheduledOnUtc = dispatch.ScheduledOnUtc;
            Dispatch.SentOnUtc = dispatch.SentOnUtc;
            Dispatch.AcknowledgedOnUtc = dispatch.AcknowledgedOnUtc;
            Dispatch.FailedOnUtc = dispatch.FailedOnUtc;
            Dispatch.FailureReason = dispatch.FailureReason;
            Dispatch.Metadata = dispatch.Metadata;
            return Task.CompletedTask;
        }

        public Task<Abstractions.Runtime.TaskDispatch> GetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(Dispatch);

        public Task<Abstractions.Runtime.TaskDispatch> TryGetById(Id dispatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(dispatchId == Dispatch.Id ? Dispatch : null!);

        public Task<Abstractions.Runtime.TaskDispatch> GetByCommandId(string commandId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Abstractions.Runtime.TaskDispatch> GetByAttemptId(Id taskExecutionAttemptId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<Abstractions.Runtime.TaskDispatch>> GetScheduledOlderThan(DateTime dueBeforeUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Abstractions.Runtime.TaskDispatch>>(Array.Empty<Abstractions.Runtime.TaskDispatch>());
    }

    private sealed class CapturingCompensationRepository : ICompensationExecutionRepository
    {
        public Abstractions.Runtime.CompensationExecution Compensation { get; private set; } = new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = Id.New(),
            SourceTaskExecutionId = Id.New(),
            CompensationTaskKey = "compensate:charge-payment",
            Status = "Started",
            Metadata = new Dictionary<string, JsonNode>()
        };

        public Task Create(Abstractions.Runtime.CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(Abstractions.Runtime.CompensationExecution compensationExecution, CancellationToken cancellationToken = default)
        {
            Compensation = compensationExecution;
            return Task.CompletedTask;
        }

        public Task<Abstractions.Runtime.CompensationExecution> TryGetById(Id compensationExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult(compensationExecutionId == Compensation.Id ? Compensation : null!);

        public Task<IReadOnlyCollection<Abstractions.Runtime.CompensationExecution>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Abstractions.Runtime.CompensationExecution>>([Compensation]);

        public Task<IReadOnlyCollection<Abstractions.Runtime.CompensationExecution>> GetPending(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Abstractions.Runtime.CompensationExecution>>([]);
    }

    private sealed class CapturingInstanceRepository : IOrchestrationInstanceRepository
    {
        public Abstractions.Runtime.OrchestrationInstance Instance { get; private set; } = new()
        {
            Id = Id.New(),
            EnvironmentKey = "local",
            OrchestrationDefinitionKey = "order.fulfillment",
            CorrelationId = "corr-1",
            ExecutionKey = "order.fulfillment::corr-1",
            Status = OrchestrationInstanceStatus.Compensating,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };

        public Task Create(Abstractions.Runtime.OrchestrationInstance instance, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Update(Abstractions.Runtime.OrchestrationInstance instance, CancellationToken cancellationToken = default)
        {
            Instance = instance;
            return Task.CompletedTask;
        }

        public Task<Abstractions.Runtime.OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult(instanceId == Instance.Id ? Instance : null!);

        public Task<OrchestrationInstanceLease> TryAcquireLease(Id instanceId, string leaseId, DateTime nowUtc, DateTime expiresOnUtc, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ReleaseLease(Id instanceId, string leaseId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<Abstractions.Runtime.OrchestrationInstance>> GetRecent(string environmentKey, int take = 50, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RuntimeInstanceSummary> GetSummary(string environmentKey, DateTime recentSinceUtc, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class CapturingTransitionRepository : IExecutionTransitionRepository
    {
        public List<Abstractions.Runtime.ExecutionTransition> Transitions { get; } = new();

        public Task Create(Abstractions.Runtime.ExecutionTransition transition, CancellationToken cancellationToken = default)
        {
            Transitions.Add(transition);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Abstractions.Runtime.ExecutionTransition>> GetByInstanceId(Id instanceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Abstractions.Runtime.ExecutionTransition>>(Transitions);

        public Task<IReadOnlyCollection<Abstractions.Runtime.ExecutionTransition>> GetRecent(string environmentKey, int take = 250, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(string environmentKey, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
