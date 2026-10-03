namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using NSubstitute;

public sealed class RetryDecisionHandlerIdempotencyTests
{
    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var dispatcher = Substitute.For<ITaskAttemptDispatcher>();

        Assert.Equal("instanceRepository", Assert.Throws<ArgumentNullException>(() => new RetryDecisionHandler(
            null!,
            taskRepository,
            dispatcher)).ParamName);
        Assert.Equal("taskRepository", Assert.Throws<ArgumentNullException>(() => new RetryDecisionHandler(
            instanceRepository,
            null!,
            dispatcher)).ParamName);
        Assert.Equal("taskAttemptDispatcher", Assert.Throws<ArgumentNullException>(() => new RetryDecisionHandler(
            instanceRepository,
            taskRepository,
            null!)).ParamName);
    }

    [Fact]
    public async Task HandleAsync_WhenRetryDecisionIsCurrent_DispatchesRetryAttempt()
    {
        var instance = CreateInstance();
        var task = CreateTaskExecution(instance, TaskExecutionStatus.Failed, lastAttemptNumber: 1);
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var taskAttemptDispatcher = Substitute.For<ITaskAttemptDispatcher>();
        var handler = new RetryDecisionHandler(
            instanceRepository,
            taskRepository,
            taskAttemptDispatcher);
        instanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(task));
        var decision = new RetryDecision(
            instance.Id,
            task.StageExecutionId,
            task.Id,
            "payment",
            CreateMessagingTask(maxRetries: 2),
            """{"saleId":"sale-1"}""");

        await handler.HandleAsync(decision);

        await taskAttemptDispatcher.Received(1).DispatchAsync(
            Arg.Is<TaskAttemptDispatchRequest>(request =>
                request.Kind == TaskAttemptDispatchKind.Retry &&
                request.Instance == instance &&
                request.TaskExecution == task &&
                request.StageKey == "payment" &&
                request.Payload == """{"saleId":"sale-1"}"""),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRetryDecisionIsStale_DoesNotCreateAnotherAttempt()
    {
        var instance = CreateInstance();
        var task = CreateTaskExecution(instance, TaskExecutionStatus.Running, lastAttemptNumber: 2);
        var instanceRepository = Substitute.For<IOrchestrationInstanceRepository>();
        var taskRepository = Substitute.For<ITaskExecutionRepository>();
        var taskAttemptDispatcher = Substitute.For<ITaskAttemptDispatcher>();
        var handler = new RetryDecisionHandler(
            instanceRepository,
            taskRepository,
            taskAttemptDispatcher);
        instanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(task));
        var decision = new RetryDecision(
            instance.Id,
            task.StageExecutionId,
            task.Id,
            "payment",
            CreateMessagingTask(),
            """{"saleId":"sale-1"}""");

        await handler.HandleAsync(decision);

        await taskAttemptDispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default);
    }

    [Fact]
    public void PrivateRetryHelpersCoverSchedulingAndCurrentStateBranches()
    {
        var now = new DateTime(2026, 8, 20, 16, 0, 0, DateTimeKind.Utc);
        var instance = CreateInstance();
        var failedTask = CreateTaskExecution(instance, TaskExecutionStatus.Failed, lastAttemptNumber: 1);
        var timedOutTask = CreateTaskExecution(instance, TaskExecutionStatus.TimedOut, lastAttemptNumber: 1);
        var completedTask = CreateTaskExecution(instance, TaskExecutionStatus.Completed, lastAttemptNumber: 1);
        var fixedTask = CreateMessagingTask(maxRetries: 2, delay: Duration.FromSeconds(5));
        var immediateTask = CreateMessagingTask(maxRetries: 2, delay: new Duration(TimeSpan.Zero));
        var customStrategyTask = CreateMessagingTask(
            maxRetries: 2,
            retryPolicy: new RetryPolicyArtifact(
                2,
                RetryStrategyType.Custom,
                Substitute.For<IRetryStrategyArtifact>(),
                ["PaymentTemporaryFailure"],
                true));
        var timeoutRetryPolicy = new RetryPolicyArtifact(
            2,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(7)),
            ["PaymentTimeout"],
            true);
        var timeoutTask = CreateMessagingTask(maxRetries: 1) with
        {
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Reconcile,
                new ReconcileTimeoutBehaviorPolicyArtifact(
                    OrchestrationActionOnTimeout.Block,
                    timeoutRetryPolicy))
        };
        var timedOutWithoutReconcileTask = CreateMessagingTask(maxRetries: 2);
        var exhausted = CreateTaskExecution(instance, TaskExecutionStatus.Failed, lastAttemptNumber: 3);

        Assert.Equal(new DateTimeOffset(now, TimeSpan.Zero).AddSeconds(5), InvokeRetryHandlerPrivateStatic<DateTimeOffset>(
            "ResolveRetryScheduledOnUtc",
            failedTask,
            fixedTask,
            now));
        Assert.Equal(new DateTimeOffset(now, TimeSpan.Zero), InvokeRetryHandlerPrivateStatic<DateTimeOffset>(
            "ResolveRetryScheduledOnUtc",
            failedTask,
            immediateTask,
            now));
        Assert.Equal(new DateTimeOffset(now, TimeSpan.Zero), InvokeRetryHandlerPrivateStatic<DateTimeOffset>(
            "ResolveRetryScheduledOnUtc",
            failedTask,
            customStrategyTask,
            now));
        Assert.Same(timeoutRetryPolicy, InvokeRetryHandlerPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            timedOutTask,
            timeoutTask));
        Assert.Same(timedOutWithoutReconcileTask.RetryPolicy, InvokeRetryHandlerPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            timedOutTask,
            timedOutWithoutReconcileTask));
        Assert.Null(InvokeRetryHandlerPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            timedOutTask,
            null!));
        Assert.Same(fixedTask.RetryPolicy, InvokeRetryHandlerPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            failedTask,
            fixedTask));
        Assert.True(InvokeRetryHandlerPrivateStatic<bool>(
            "CanRetryCurrentTaskState",
            failedTask,
            fixedTask));
        Assert.True(InvokeRetryHandlerPrivateStatic<bool>(
            "CanRetryCurrentTaskState",
            timedOutTask,
            timeoutTask));
        Assert.True(InvokeRetryHandlerPrivateStatic<bool>(
            "CanRetryCurrentTaskState",
            timedOutTask,
            timedOutWithoutReconcileTask));
        Assert.False(InvokeRetryHandlerPrivateStatic<bool>(
            "CanRetryCurrentTaskState",
            completedTask,
            fixedTask));
        Assert.False(InvokeRetryHandlerPrivateStatic<bool>(
            "CanRetryCurrentTaskState",
            exhausted,
            fixedTask));
        Assert.False(InvokeRetryHandlerPrivateStatic<bool>(
            "CanRetryCurrentTaskState",
            failedTask,
            fixedTask with { RetryPolicy = null! }));
    }

    private static OrchestrationInstance CreateInstance()
    {
        var id = Id.New();
        return new OrchestrationInstance
        {
            Id = id,
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "sale-1",
            SagaId = id.ToString(),
            ExecutionKey = "sales.sale.created:sale-1",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
    }

    private static TaskExecution CreateTaskExecution(
        OrchestrationInstance instance,
        TaskExecutionStatus status,
        int lastAttemptNumber)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "payments.capture",
            TaskKind = TaskKind.Messaging,
            Status = status,
            LastAttemptNumber = lastAttemptNumber
        };

    private static TaskArtifact CreateMessagingTask(
        int maxRetries = 1,
        Duration? delay = null,
        RetryPolicyArtifact? retryPolicy = null)
    {
        retryPolicy ??= new RetryPolicyArtifact(
            maxRetries,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(delay ?? Duration.FromSeconds(1)),
            ["PaymentTemporaryFailure"],
            true);

        return new TaskArtifact(
            Id.New(),
            "payments.capture",
            "Capture payment",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Parallel,
            null,
            null,
            new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact()),
            new MessagingTaskConfigurationArtifact("payments.capture", new SemanticVersion(1, 0, 0), null),
            retryPolicy,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWait,
            true);
    }

    private static T? InvokeRetryHandlerPrivateStatic<T>(string methodName, params object?[] args)
    {
        var method = Assert.Single(typeof(RetryDecisionHandler)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == args.Length));
        return (T?)method.Invoke(null, args);
    }
}
