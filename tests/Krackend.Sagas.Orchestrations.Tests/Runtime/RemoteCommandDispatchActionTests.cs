namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Distribution;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mule;
using NSubstitute;

public sealed class RemoteCommandDispatchActionTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCallbackCompletesBeforePublishConfirmation_DoesNotDowngradeTaskState()
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = Id.New().ToString(),
            SagaId = Id.New().ToString(),
            ExecutionKey = "sales.sale.created",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "sales.complete",
            Status = TaskExecutionStatus.Running
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "FireAndWaitCallback",
            DispatchStatus = "Enqueued"
        };
        var executor = Substitute.For<IRemoteCommandExecutor>();
        executor
            .ExecuteAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Assert.Equal(TaskExecutionStatus.WaitingResponse, task.Status);
                Assert.Equal(TaskExecutionStatus.WaitingResponse, attempt.Status);
                Assert.Equal(OrchestrationInstanceStatus.Waiting, instance.Status);
                Assert.Equal("WaitingResponse", dispatch.DispatchStatus);

                task.Status = TaskExecutionStatus.Completed;
                task.WaitingSinceUtc = null;
                task.CompletedOnUtc = DateTime.UtcNow;
                attempt.Status = TaskExecutionStatus.Completed;
                attempt.WaitingSinceUtc = null;
                attempt.CompletedOnUtc = DateTime.UtcNow;
                dispatch.DispatchStatus = "Acknowledged";
                dispatch.AcknowledgedOnUtc = DateTime.UtcNow;
                instance.Status = OrchestrationInstanceStatus.Running;
                instance.WaitingSinceUtc = null;
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddKeyedSingleton(RemoteCommandTransport.Messaging, executor);
        var provider = services.BuildServiceProvider();
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var attemptRepository = Substitute.For<ITaskExecutionAttemptRepository>();
        var dispatchRepository = Substitute.For<ITaskDispatchRepository>();
        var transitionRepository = Substitute.For<IExecutionTransitionRepository>();
        instanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        attemptRepository.GetById(attempt.Id, Arg.Any<CancellationToken>()).Returns(attempt);
        dispatchRepository.GetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(dispatch);
        var action = new RemoteCommandDispatchAction(
            provider,
            Substitute.For<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            instanceRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            transitionRepository,
            new DefaultMuleTerminalFailureMarker());
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}",
            OrchestrationInstanceId = instance.Id.ToString(),
            StageExecutionId = task.StageExecutionId.ToString(),
            TaskExecutionId = task.Id.ToString(),
            TaskExecutionAttemptId = attempt.Id.ToString(),
            DispatchId = dispatch.Id.ToString(),
            AwaitResponse = true
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = instance.CorrelationId,
                DeduplicationKey = dispatch.Id.ToString(),
                Status = DurableActionStatus.Pending
            },
            provider,
            command);

        await action.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(TaskExecutionStatus.Completed, task.Status);
        Assert.Equal(TaskExecutionStatus.Completed, attempt.Status);
        Assert.Equal("Acknowledged", dispatch.DispatchStatus);
        Assert.NotNull(dispatch.SentOnUtc);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        await transitionRepository.Received(1).Create(
            Arg.Is<ExecutionTransition>(transition =>
                transition.TransitionType == "TaskDispatched" &&
                transition.ToStatus == TaskExecutionStatus.WaitingResponse.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransportExecutorIsMissing_MarksActionAsTerminal()
    {
        var action = new RemoteCommandDispatchAction(
            new ServiceCollection().BuildServiceProvider(),
            Substitute.For<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            Substitute.For<IOrchestrationInstanceRepository>(),
            Substitute.For<ITaskExecutionRepository>(),
            Substitute.For<ITaskExecutionAttemptRepository>(),
            Substitute.For<ITaskDispatchRepository>(),
            Substitute.For<IExecutionTransitionRepository>(),
            new DefaultMuleTerminalFailureMarker());

        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}"
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = "test",
                DeduplicationKey = "test",
                Status = DurableActionStatus.Pending
            },
            new ServiceCollection().BuildServiceProvider(),
            command);

        await Assert.ThrowsAsync<RemoteCommandConfigurationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(int.MaxValue - 1, context.Action.Attempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransportPublishFailsTransiently_MarksDispatchRetryPendingWithoutFailingExecution()
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = Id.New().ToString(),
            SagaId = Id.New().ToString(),
            ExecutionKey = "sales.sale.created",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "inventories.reserve",
            Status = TaskExecutionStatus.Running
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "FireAndWaitCallback",
            DispatchStatus = "Enqueued"
        };
        var executor = Substitute.For<IRemoteCommandExecutor>();
        executor
            .ExecuteAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TimeoutException("Rabbit publish timed out.")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton(RemoteCommandTransport.Messaging, executor);
        var provider = services.BuildServiceProvider();
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var attemptRepository = Substitute.For<ITaskExecutionAttemptRepository>();
        var dispatchRepository = Substitute.For<ITaskDispatchRepository>();
        var transitionRepository = Substitute.For<IExecutionTransitionRepository>();
        instanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        attemptRepository.GetById(attempt.Id, Arg.Any<CancellationToken>()).Returns(attempt);
        dispatchRepository.GetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(dispatch);
        var action = new RemoteCommandDispatchAction(
            provider,
            Substitute.For<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            instanceRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            transitionRepository,
            new DefaultMuleTerminalFailureMarker());
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}",
            OrchestrationInstanceId = instance.Id.ToString(),
            StageExecutionId = task.StageExecutionId.ToString(),
            TaskExecutionId = task.Id.ToString(),
            TaskExecutionAttemptId = attempt.Id.ToString(),
            DispatchId = dispatch.Id.ToString(),
            AwaitResponse = true
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = instance.CorrelationId,
                DeduplicationKey = dispatch.Id.ToString(),
                Status = DurableActionStatus.Pending
            },
            provider,
            command);

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal("RetryPending", dispatch.DispatchStatus);
        Assert.NotNull(dispatch.FailedOnUtc);
        Assert.Equal("Rabbit publish timed out.", dispatch.FailureReason);
        Assert.Equal(TaskExecutionStatus.Running, task.Status);
        Assert.Null(task.WaitingSinceUtc);
        Assert.Null(task.FailedOnUtc);
        Assert.Equal(TaskExecutionStatus.Running, attempt.Status);
        Assert.Null(attempt.WaitingSinceUtc);
        Assert.Null(attempt.FailedOnUtc);
        Assert.Null(attempt.ErrorCode);
        Assert.Null(attempt.ErrorMessage);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.Null(instance.WaitingSinceUtc);
        Assert.Null(instance.FailedOnUtc);
        Assert.NotEqual(int.MaxValue - 1, context.Action.Attempts);
        await transitionRepository.Received(1).Create(
            Arg.Is<ExecutionTransition>(transition =>
                transition.TransitionType == "TaskDispatchRetryPending" &&
                transition.FromStatus == "WaitingResponse" &&
                transition.ToStatus == "RetryPending"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallbackCompletesBeforeLateTransportFailure_DoesNotDowngradeExecution()
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = Id.New().ToString(),
            SagaId = Id.New().ToString(),
            ExecutionKey = "sales.sale.created",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "payments.charge",
            Status = TaskExecutionStatus.Running
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "FireAndWaitCallback",
            DispatchStatus = "Enqueued"
        };
        var executor = Substitute.For<IRemoteCommandExecutor>();
        executor
            .ExecuteAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                task.Status = TaskExecutionStatus.Completed;
                task.WaitingSinceUtc = null;
                task.CompletedOnUtc = DateTime.UtcNow;
                attempt.Status = TaskExecutionStatus.Completed;
                attempt.WaitingSinceUtc = null;
                attempt.CompletedOnUtc = DateTime.UtcNow;
                dispatch.DispatchStatus = "Acknowledged";
                dispatch.AcknowledgedOnUtc = DateTime.UtcNow;
                instance.Status = OrchestrationInstanceStatus.Running;
                instance.WaitingSinceUtc = null;
                return Task.FromException(new InvalidOperationException("Late publish confirmation failed."));
            });

        var services = new ServiceCollection();
        services.AddKeyedSingleton(RemoteCommandTransport.Messaging, executor);
        var provider = services.BuildServiceProvider();
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var attemptRepository = Substitute.For<ITaskExecutionAttemptRepository>();
        var dispatchRepository = Substitute.For<ITaskDispatchRepository>();
        var transitionRepository = Substitute.For<IExecutionTransitionRepository>();
        instanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        attemptRepository.GetById(attempt.Id, Arg.Any<CancellationToken>()).Returns(attempt);
        dispatchRepository.GetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(dispatch);
        var action = new RemoteCommandDispatchAction(
            provider,
            Substitute.For<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            instanceRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            transitionRepository,
            new DefaultMuleTerminalFailureMarker());
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}",
            OrchestrationInstanceId = instance.Id.ToString(),
            StageExecutionId = task.StageExecutionId.ToString(),
            TaskExecutionId = task.Id.ToString(),
            TaskExecutionAttemptId = attempt.Id.ToString(),
            DispatchId = dispatch.Id.ToString(),
            AwaitResponse = true
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = instance.CorrelationId,
                DeduplicationKey = dispatch.Id.ToString(),
                Status = DurableActionStatus.Pending
            },
            provider,
            command);

        await action.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(TaskExecutionStatus.Completed, task.Status);
        Assert.Equal(TaskExecutionStatus.Completed, attempt.Status);
        Assert.Equal("Acknowledged", dispatch.DispatchStatus);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.NotEqual(int.MaxValue - 1, context.Action.Attempts);
        await transitionRepository.Received(1).Create(
            Arg.Is<ExecutionTransition>(transition =>
                transition.TransitionType == "TaskDispatchFailureIgnored" &&
                transition.FromStatus == "Acknowledged" &&
                transition.ToStatus == "Acknowledged"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskTimesOutBeforeLateTransportFailure_DoesNotReopenDispatch()
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = Id.New().ToString(),
            SagaId = Id.New().ToString(),
            ExecutionKey = "sales.sale.created",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "payments.charge",
            Status = TaskExecutionStatus.Running
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Running
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "FireAndWaitCallback",
            DispatchStatus = "Enqueued"
        };
        var executor = Substitute.For<IRemoteCommandExecutor>();
        executor
            .ExecuteAsync(Arg.Any<RemoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                task.Status = TaskExecutionStatus.TimedOut;
                task.WaitingSinceUtc = null;
                task.TimedOutOnUtc = DateTime.UtcNow;
                attempt.Status = TaskExecutionStatus.TimedOut;
                attempt.WaitingSinceUtc = null;
                attempt.TimedOutOnUtc = DateTime.UtcNow;
                attempt.ErrorCode = "TIMEOUT";
                dispatch.DispatchStatus = "TimedOut";
                dispatch.FailedOnUtc = DateTime.UtcNow;
                dispatch.FailureReason = "Task timed out.";
                instance.Status = OrchestrationInstanceStatus.Failed;
                instance.WaitingSinceUtc = null;
                return Task.FromException(new InvalidOperationException("Late publish confirmation failed."));
            });

        var services = new ServiceCollection();
        services.AddKeyedSingleton(RemoteCommandTransport.Messaging, executor);
        var provider = services.BuildServiceProvider();
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var attemptRepository = Substitute.For<ITaskExecutionAttemptRepository>();
        var dispatchRepository = Substitute.For<ITaskDispatchRepository>();
        var transitionRepository = Substitute.For<IExecutionTransitionRepository>();
        instanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(instance);
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        attemptRepository.GetById(attempt.Id, Arg.Any<CancellationToken>()).Returns(attempt);
        dispatchRepository.GetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(dispatch);
        var action = new RemoteCommandDispatchAction(
            provider,
            Substitute.For<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            instanceRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            transitionRepository,
            new DefaultMuleTerminalFailureMarker());
        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            Payload = "{}",
            OrchestrationInstanceId = instance.Id.ToString(),
            StageExecutionId = task.StageExecutionId.ToString(),
            TaskExecutionId = task.Id.ToString(),
            TaskExecutionAttemptId = attempt.Id.ToString(),
            DispatchId = dispatch.Id.ToString(),
            AwaitResponse = true
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = instance.CorrelationId,
                DeduplicationKey = dispatch.Id.ToString(),
                Status = DurableActionStatus.Pending
            },
            provider,
            command);

        await action.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(TaskExecutionStatus.TimedOut, task.Status);
        Assert.Equal(TaskExecutionStatus.TimedOut, attempt.Status);
        Assert.Equal("TimedOut", dispatch.DispatchStatus);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.NotEqual(int.MaxValue - 1, context.Action.Attempts);
        await transitionRepository.Received(1).Create(
            Arg.Is<ExecutionTransition>(transition =>
                transition.TransitionType == "TaskDispatchFailureIgnored" &&
                transition.FromStatus == "TimedOut" &&
                transition.ToStatus == "TimedOut"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessagingAdapterIsMissing_MarksActionAsTerminal()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        var provider = services.BuildServiceProvider();

        var action = new RemoteCommandDispatchAction(
            provider,
            provider.GetRequiredService<Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata.IOrchestrationMessageMetadataSetter>(),
            Substitute.For<IOrchestrationInstanceRepository>(),
            Substitute.For<ITaskExecutionRepository>(),
            Substitute.For<ITaskExecutionAttemptRepository>(),
            Substitute.For<ITaskDispatchRepository>(),
            Substitute.For<IExecutionTransitionRepository>(),
            new DefaultMuleTerminalFailureMarker());

        var command = new RemoteCommand
        {
            RemoteCommandTransport = RemoteCommandTransport.Messaging,
            SettingsPayload = """{"topic":"inventory.reserve","version":"1.0.0","payload":"{}"}""",
            Payload = """{"saleId":"sale-1"}"""
        };
        var context = new MuleActionContext<RemoteCommand>(
            new DurableAction
            {
                Key = ActionKey.From("RemoteCommandDispatch"),
                Lane = "default",
                CorrelationId = "test",
                DeduplicationKey = "test",
                Status = DurableActionStatus.Pending
            },
            provider,
            command);

        await Assert.ThrowsAsync<RemoteCommandConfigurationException>(
            async () => await action.ExecuteAsync(context, CancellationToken.None));

        Assert.Equal(int.MaxValue - 1, context.Action.Attempts);
    }
}
