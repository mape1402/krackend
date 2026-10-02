namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

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
    public async Task HandleAsync_WhenRetryDecisionIsStale_DoesNotCreateAnotherAttempt()
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "sale-1",
            SagaId = Id.New().ToString(),
            ExecutionKey = "sales.sale.created:sale-1",
            Status = OrchestrationInstanceStatus.Running,
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "payments.capture",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Running,
            LastAttemptNumber = 2
        };
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

    private static TaskArtifact CreateMessagingTask()
    {
        var retryPolicy = new RetryPolicyArtifact(
            1,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
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
}
