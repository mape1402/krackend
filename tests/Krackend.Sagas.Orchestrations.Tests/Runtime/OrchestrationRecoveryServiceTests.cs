namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using NSubstitute;

public sealed class OrchestrationRecoveryServiceTests
{
    [Fact]
    public async Task ReplayRejectsInvalidInstanceId()
    {
        var fixture = new RecoveryFixture();

        var result = await fixture.Service.ReplayAsync("not-a-valid-id");

        Assert.False(result.Succeeded);
        Assert.Equal(string.Empty, result.Status);
        Assert.Contains("invalid", result.Message, StringComparison.OrdinalIgnoreCase);
        await fixture.InstanceRepository.DidNotReceiveWithAnyArgs().GetById(default, default);
    }

    [Theory]
    [InlineData(OrchestrationInstanceStatus.Running)]
    [InlineData(OrchestrationInstanceStatus.Idle)]
    [InlineData(OrchestrationInstanceStatus.Completed)]
    public async Task RecoverTerminalRejectsSagasThatAreNotTerminalFailures(OrchestrationInstanceStatus status)
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(status);
        fixture.InstanceRepository
            .GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));

        var result = await fixture.Service.RecoverTerminalAsync(instance.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal(status.ToString(), result.Status);
        Assert.Contains("not in a recoverable status", result.Message, StringComparison.OrdinalIgnoreCase);
        await fixture.ArtifactResolver.DidNotReceiveWithAnyArgs().ResolveAsync(default, default);
    }

    [Fact]
    public async Task RecoverTerminalRejectsMissingSagasWithoutResolvingAnArtifact()
    {
        var fixture = new RecoveryFixture();
        var id = Id.New();
        fixture.InstanceRepository
            .GetById(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OrchestrationInstance>(null));

        var result = await fixture.Service.RecoverTerminalAsync(id.ToString());

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        await fixture.ArtifactResolver.DidNotReceiveWithAnyArgs().ResolveAsync(default, default);
    }

    [Fact]
    public async Task RecoverTerminalUsesTheSameCheckpointedReplayForDeadLetteredSagas()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            Array.Empty<StageExecution>(),
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.RecoverTerminalAsync(instance.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Running.ToString(), result.Status);
        Assert.Single(fixture.Forwards);
        Assert.Equal("InstanceReplayRequested", Assert.Single(fixture.Transitions).TransitionType);
    }

    [Fact]
    public async Task AbortRejectsFinalInstance()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Completed);
        fixture.InstanceRepository
            .GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));

        var result = await fixture.Service.AbortAsync(instance.Id.ToString(), "operator stop");

        Assert.False(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Completed.ToString(), result.Status);
        await fixture.InstanceRepository.DidNotReceiveWithAnyArgs().Update(default!, default);
        await fixture.TransitionRepository.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task AbortMarksRecoverableInstanceAsAborted()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.DeadLettered,
            failedOnUtc: null,
            stoppedOnUtc: null,
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-5),
            errorSummary: "timeout");
        fixture.InstanceRepository
            .GetById(instance.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(instance));

        var result = await fixture.Service.AbortAsync(instance.Id.ToString(), "  operator fixed externally  ");

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Aborted.ToString(), result.Status);
        Assert.Equal(OrchestrationInstanceStatus.Aborted, instance.Status);
        Assert.NotNull(instance.FailedOnUtc);
        Assert.NotNull(instance.StoppedOnUtc);
        Assert.Null(instance.WaitingSinceUtc);
        Assert.Equal("operator fixed externally", instance.ErrorSummary);
        Assert.Equal("operator fixed externally", instance.Metadata["Recovery.AbortReason"]!.GetValue<string>());
        Assert.NotNull(instance.Metadata["Recovery.AbortedOnUtc"]);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("InstanceAborted", transition.TransitionType);
        Assert.Equal(OrchestrationInstanceStatus.DeadLettered.ToString(), transition.FromStatus);
        Assert.Equal(OrchestrationInstanceStatus.Aborted.ToString(), transition.ToStatus);
        await fixture.DispatchCancellationCoordinator.Received(1).CancelPendingAsync(
            instance,
            "operator fixed externally",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayFailedTaskDispatchesRetryFromArtifactDefinition()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.DeadLettered,
            currentStageKey: "stage-one",
            currentTaskKey: "reserve-stock",
            errorSummary: "task failed");
        var stageExecution = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "stage-one",
            Order = 1,
            Status = StageExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-2),
            ErrorSummary = "stage failed"
        };
        var taskExecution = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stageExecution.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastAttemptNumber = 2,
            CorrelationId = "task-correlation",
            Metadata =
            {
                ["RetrySuppressed"] = JsonValue.Create(true)
            }
        };
        var taskArtifact = MessagingTask("reserve-stock");
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", taskArtifact)),
            [stageExecution],
            [taskExecution]);

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Running.ToString(), result.Status);
        Assert.False(taskExecution.Metadata.ContainsKey("RetrySuppressed"));
        Assert.True(taskExecution.Metadata["ManualReplay"]!.GetValue<bool>());
        Assert.Equal("operator", instance.Metadata["Recovery.ReplayRequestedBy"]!.GetValue<string>());
        var dispatch = Assert.Single(fixture.Dispatches);
        Assert.Equal(TaskAttemptDispatchKind.Retry, dispatch.Kind);
        Assert.Same(instance, dispatch.Instance);
        Assert.Equal(stageExecution.Id, dispatch.StageExecutionId);
        Assert.Equal("stage-one", dispatch.StageKey);
        Assert.Same(taskArtifact, dispatch.Task);
        Assert.Same(taskExecution, dispatch.TaskExecution);
        Assert.Contains("snapshot", dispatch.Payload, StringComparison.Ordinal);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("TaskReplayRequested", transition.TransitionType);
        Assert.Equal(TaskExecutionStatus.Failed.ToString(), transition.FromStatus);
        Assert.Equal(TaskExecutionStatus.Running.ToString(), transition.ToStatus);
        Assert.Equal(stageExecution.Id, transition.StageExecutionId);
        Assert.Equal(taskExecution.Id, transition.TaskExecutionId);
    }

    [Fact]
    public async Task ReplaySpecificTimedOutTaskCreatesANewAttemptAndRetainsTheOperatorReason()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered, currentTaskKey: "reserve-stock");
        var stage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "stage-one",
            Order = 1,
            Status = StageExecutionStatus.Failed
        };
        var timedOutTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = stage.Id,
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.TimedOut,
            TimedOutOnUtc = DateTime.UtcNow.AddMinutes(-1),
            LastAttemptNumber = 1
        };
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            [stage],
            [timedOutTask]);
        fixture.TaskRepository
            .GetById(timedOutTask.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(timedOutTask));

        var result = await fixture.Service.ReplayTaskAsync(
            instance.Id.ToString(),
            timedOutTask.Id.ToString(),
            "{\"value\":\"operator-payload\"}",
            "  service is available again  ");

        Assert.True(result.Succeeded);
        var dispatch = Assert.Single(fixture.Dispatches);
        Assert.Equal(TaskAttemptDispatchKind.Retry, dispatch.Kind);
        Assert.Equal("operator-payload", JsonNode.Parse(dispatch.Payload)!["value"]!.GetValue<string>());
        Assert.Equal("service is available again", timedOutTask.Metadata["Recovery.TaskReplayReason"]!.GetValue<string>());
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("TaskReplayRequested", transition.TransitionType);
        Assert.Equal(TaskExecutionStatus.TimedOut.ToString(), transition.FromStatus);
        Assert.Equal(TaskExecutionStatus.Running.ToString(), transition.ToStatus);
        Assert.Equal(timedOutTask.Id, transition.TaskExecutionId);
    }

    [Fact]
    public async Task ReplaySpecificTaskRejectsATaskThatDoesNotBelongToTheInstance()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var unrelatedTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = Id.New(),
            StageExecutionId = Id.New(),
            TaskKey = "reserve-stock",
            Status = TaskExecutionStatus.Failed
        };
        fixture.InstanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(instance));
        fixture.TaskRepository.GetById(unrelatedTask.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(unrelatedTask));

        var result = await fixture.Service.ReplayTaskAsync(instance.Id.ToString(), unrelatedTask.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Contains("does not belong", result.Message, StringComparison.OrdinalIgnoreCase);
        await fixture.ArtifactResolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
        Assert.Empty(fixture.Dispatches);
    }

    [Fact]
    public async Task ReplaySpecificTaskRejectsANonTerminalTask()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Idle);
        var waitingTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "reserve-stock",
            Status = TaskExecutionStatus.WaitingResponse
        };
        fixture.InstanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(instance));
        fixture.TaskRepository.GetById(waitingTask.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(waitingTask));

        var result = await fixture.Service.ReplayTaskAsync(instance.Id.ToString(), waitingTask.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal(TaskExecutionStatus.WaitingResponse.ToString(), result.Status);
        Assert.Contains("cannot be replayed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Dispatches);
    }

    [Fact]
    public async Task ReplaySpecificTaskRejectsInvalidTaskId()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);

        var result = await fixture.Service.ReplayTaskAsync(instance.Id.ToString(), "not-a-valid-id");

        Assert.False(result.Succeeded);
        Assert.Contains("Task execution id is invalid", result.Message, StringComparison.Ordinal);
        await fixture.InstanceRepository.DidNotReceiveWithAnyArgs().GetById(default, default);
    }

    [Fact]
    public async Task ReplaySpecificFailedDispatchSchedulesTheProviderWithoutCreatingANewTaskAttempt()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var task = CreateFailedTask(instance.Id);
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Failed
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "Failed"
        };
        fixture.ArrangeDispatchReplay(instance, task, attempt, dispatch);
        fixture.DispatchReplayService
            .ReplayAsync(Arg.Any<TaskDispatchReplayRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchReplayResult.Success("Scheduled", "Delivery replay was scheduled.", "provider-action-1")));

        var result = await fixture.Service.ReplayDispatchAsync(
            instance.Id.ToString(),
            task.Id.ToString(),
            dispatch.Id.ToString(),
            "dependency recovered");

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.Equal(TaskExecutionStatus.Running, task.Status);
        Assert.Equal(TaskExecutionStatus.Running, attempt.Status);
        Assert.Equal("ReplayPending", dispatch.DispatchStatus);
        Assert.False(task.Metadata.ContainsKey("ManualReplay"));
        Assert.NotNull(dispatch.Metadata["Recovery.DispatchReplayRequestId"]);
        Assert.Equal("dependency recovered", dispatch.Metadata["Recovery.DispatchReplayReason"]!.GetValue<string>());
        Assert.Equal("provider-action-1", dispatch.Metadata["Recovery.DispatchReplayProviderActionId"]!.GetValue<string>());
        Assert.Equal(["TaskDispatchReplayRequested", "TaskDispatchReplayScheduled"], fixture.Transitions.Select(x => x.TransitionType));
        await fixture.TaskAttemptDispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default);
    }

    [Fact]
    public async Task ReplaySpecificFailedDispatchRestoresFailureWhenTheProviderRejectsIt()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var task = CreateFailedTask(instance.Id);
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Failed
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "Failed"
        };
        fixture.ArrangeDispatchReplay(instance, task, attempt, dispatch);
        fixture.DispatchReplayService
            .ReplayAsync(Arg.Any<TaskDispatchReplayRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchReplayResult.Rejected("NotEligible", "The source delivery is not eligible.")));

        var result = await fixture.Service.ReplayDispatchAsync(instance.Id.ToString(), task.Id.ToString(), dispatch.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.DeadLettered, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal(TaskExecutionStatus.Failed, attempt.Status);
        Assert.Equal("Failed", dispatch.DispatchStatus);
        Assert.Equal("NotEligible", dispatch.Metadata["Recovery.DispatchReplayProviderStatus"]!.GetValue<string>());
        Assert.Equal(["TaskDispatchReplayRequested", "TaskDispatchReplayRejected"], fixture.Transitions.Select(x => x.TransitionType));
    }

    [Fact]
    public async Task ReplaySpecificPendingDispatchResumesWithTheSameIdempotencyKey()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Running);
        var task = CreateFailedTask(instance.Id);
        task.Status = TaskExecutionStatus.Running;
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
            DispatchType = "Messaging",
            DispatchStatus = "ReplayPending",
            Metadata =
            {
                ["Recovery.DispatchReplayRequestId"] = JsonValue.Create(Id.New().ToString())
            }
        };
        fixture.ArrangeDispatchReplay(instance, task, attempt, dispatch);
        string? requestId = null;
        fixture.DispatchReplayService
            .ReplayAsync(Arg.Do<TaskDispatchReplayRequest>(request => requestId = request.RequestId), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TaskDispatchReplayResult.Success("AlreadyScheduled", "Delivery replay is already scheduled.")));

        var result = await fixture.Service.ReplayDispatchAsync(instance.Id.ToString(), task.Id.ToString(), dispatch.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(dispatch.Metadata["Recovery.DispatchReplayRequestId"]!.GetValue<string>(), requestId);
        Assert.Single(fixture.Transitions);
        Assert.Equal("TaskDispatchReplayScheduled", fixture.Transitions[0].TransitionType);
    }

    [Fact]
    public async Task ReplaySpecificDispatchRejectsAnActiveDelivery()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Idle);
        var task = CreateFailedTask(instance.Id);
        task.Status = TaskExecutionStatus.WaitingResponse;
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            Status = TaskExecutionStatus.WaitingResponse
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchType = "Messaging",
            DispatchStatus = "WaitingResponse"
        };
        fixture.ArrangeDispatchReplay(instance, task, attempt, dispatch);

        var result = await fixture.Service.ReplayDispatchAsync(instance.Id.ToString(), task.Id.ToString(), dispatch.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Contains("not failed", result.Message, StringComparison.OrdinalIgnoreCase);
        await fixture.DispatchReplayService.DidNotReceiveWithAnyArgs().ReplayAsync(default!, default);
    }

    [Fact]
    public async Task ReplayFailedTaskRejectsWhenArtifactDefinitionIsUnavailable()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.DeadLettered);
        var failedTask = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "missing-task",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("other-stage", MessagingTask("other-task"))),
            Array.Empty<StageExecution>(),
            [failedTask]);

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString());

        Assert.False(result.Succeeded);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Dispatches);
        Assert.Empty(fixture.Transitions);
    }

    [Fact]
    public async Task ReplayFailedStageResetsStageAndContinuesEngine()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.DeadLettered,
            currentStageKey: "stage-two",
            currentTaskKey: "charge-card",
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-10),
            errorSummary: "stage failed");
        var failedStage = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = "stage-two",
            Order = 2,
            Status = StageExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-2),
            ErrorSummary = "stage failed"
        };
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-two", MessagingTask("charge-card"))),
            [failedStage],
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString(), """{"value":"operator-payload"}""");

        Assert.True(result.Succeeded);
        Assert.Equal(StageExecutionStatus.Running, failedStage.Status);
        Assert.Null(failedStage.FailedOnUtc);
        Assert.Null(failedStage.ErrorSummary);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.Equal("stage-two", instance.CurrentStageKey);
        Assert.Equal(string.Empty, instance.CurrentTaskKey);
        Assert.Null(instance.FailedOnUtc);
        Assert.Null(instance.WaitingSinceUtc);
        var forward = Assert.Single(fixture.Forwards);
        Assert.Equal(instance.RuntimeOrchestrationArtifactId.ToString(), forward.ArtifactId);
        Assert.Equal("operator-payload", forward.Payload!["value"]!.GetValue<string>());
        Assert.Equal("stage-two", forward.MessageMetadata.CurrentStage);
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("StageReplayRequested", transition.TransitionType);
        Assert.Equal(failedStage.Id, transition.StageExecutionId);
    }

    [Fact]
    public async Task ReplayRecoverableInstanceWithoutFailedWorkContinuesEngine()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.Idle,
            currentStageKey: "stage-one",
            currentTaskKey: string.Empty,
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-3),
            errorSummary: "waiting");
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            Array.Empty<StageExecution>(),
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString());

        Assert.True(result.Succeeded);
        Assert.Equal(OrchestrationInstanceStatus.Running, instance.Status);
        Assert.Null(instance.FailedOnUtc);
        Assert.Null(instance.WaitingSinceUtc);
        Assert.Null(instance.IdleSinceUtc);
        Assert.Null(instance.IdleExpiresOnUtc);
        Assert.Null(instance.ErrorSummary);
        Assert.Equal("operator", instance.Metadata["Recovery.ReplayRequestedBy"]!.GetValue<string>());
        var forward = Assert.Single(fixture.Forwards);
        Assert.Equal(instance.SagaId, forward.MessageMetadata.SagaId);
        Assert.Equal(instance.Id.ToString(), forward.MessageMetadata.OrchestrationInstanceId);
        Assert.Equal(instance.CorrelationId, forward.MessageMetadata.CorrelationId);
        Assert.Equal("snapshot", forward.Payload!["value"]!.GetValue<string>());
        var transition = Assert.Single(fixture.Transitions);
        Assert.Equal("InstanceReplayRequested", transition.TransitionType);
        Assert.Null(transition.StageExecutionId);
        Assert.Null(transition.TaskExecutionId);
    }

    [Fact]
    public async Task ReplayRecoverableInstanceUsesInstanceIdWhenSagaIdIsMissingAndCarriesCurrentTask()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(
            OrchestrationInstanceStatus.Running,
            currentStageKey: "stage-one",
            currentTaskKey: "reserve-stock",
            waitingSinceUtc: DateTime.UtcNow.AddMinutes(-2),
            errorSummary: "manual replay");
        instance.SagaId = "";
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            Array.Empty<StageExecution>(),
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString(), " ");

        Assert.True(result.Succeeded);
        var forward = Assert.Single(fixture.Forwards);
        Assert.Equal(instance.Id.ToString(), forward.MessageMetadata.SagaId);
        Assert.Equal(["reserve-stock"], forward.MessageMetadata.CurrentTasks);
        Assert.Equal("snapshot", forward.Payload!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReplayRecoverableInstanceUsesTheSuppliedPayloadInsteadOfTheStoredSnapshot()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Idle);
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            Array.Empty<StageExecution>(),
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString(), "{\"value\":\"override\"}");

        Assert.True(result.Succeeded);
        Assert.Equal("override", Assert.Single(fixture.Forwards).Payload!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReplayRecoverableInstanceWithoutASnapshotForwardsANullPayload()
    {
        var fixture = new RecoveryFixture();
        var instance = CreateInstance(OrchestrationInstanceStatus.Idle);
        instance.SnapshotPayload = null!;
        fixture.ArrangeReplay(
            instance,
            CreateResolvedArtifact(instance.RuntimeOrchestrationArtifactId, Stage("stage-one", MessagingTask("reserve-stock"))),
            Array.Empty<StageExecution>(),
            Array.Empty<TaskExecution>());

        var result = await fixture.Service.ReplayAsync(instance.Id.ToString(), " ");

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(fixture.Forwards).Payload);
    }

    private static OrchestrationInstance CreateInstance(
        OrchestrationInstanceStatus status,
        string currentStageKey = "stage-one",
        string currentTaskKey = "reserve-stock",
        DateTime? failedOnUtc = null,
        DateTime? stoppedOnUtc = null,
        DateTime? waitingSinceUtc = null,
        string errorSummary = "")
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionKey = "order.flow",
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            CorrelationId = "correlation-1",
            SagaId = "saga-1",
            ExecutionKey = "execution-1",
            Status = status,
            CurrentStageKey = currentStageKey,
            CurrentTaskKey = currentTaskKey,
            StartedOnUtc = DateTime.UtcNow.AddMinutes(-20),
            LastUpdatedOnUtc = DateTime.UtcNow.AddMinutes(-1),
            FailedOnUtc = failedOnUtc ?? (status is OrchestrationInstanceStatus.Failed or OrchestrationInstanceStatus.DeadLettered ? DateTime.UtcNow.AddMinutes(-1) : null),
            StoppedOnUtc = stoppedOnUtc,
            WaitingSinceUtc = waitingSinceUtc,
            IdleSinceUtc = status == OrchestrationInstanceStatus.Idle ? DateTime.UtcNow.AddMinutes(-3) : null,
            IdleExpiresOnUtc = status == OrchestrationInstanceStatus.Idle ? DateTime.UtcNow.AddMinutes(17) : null,
            ErrorSummary = errorSummary,
            SnapshotPayload = JsonNode.Parse("""{"value":"snapshot"}""")!
        };

    private static ResolvedOrchestrationArtifact CreateResolvedArtifact(Id artifactId, params StageArtifact[] stages)
    {
        var artifact = new OrchestrationArtifact(
            Id.New(),
            Id.New(),
            "order.flow",
            "Order Flow",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("checksum"),
            Array.Empty<TriggerBindingArtifact>(),
            Array.Empty<VariableDefinitionArtifact>(),
            stages);

        return new ResolvedOrchestrationArtifact
        {
            RuntimeArtifact = new RuntimeOrchestrationArtifact
            {
                Id = artifactId,
                OrchestrationDefinitionKey = artifact.Key,
                ArtifactType = "orchestration.deploy",
                Version = artifact.Version,
                ArtifactChecksum = artifact.Checksum,
                ArtifactPayload = JsonNode.Parse("""{}""")!
            },
            Artifact = artifact
        };
    }

    private static StageArtifact Stage(string key, params TaskArtifact[] tasks)
        => new(
            Id.New(),
            key,
            key,
            1,
            true,
            null!,
            tasks,
            Array.Empty<ParallelGroupArtifact>(),
            Array.Empty<BranchRuleArtifact>());

    private static TaskArtifact MessagingTask(string key)
        => new(
            Id.New(),
            key,
            key,
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null!,
            null!,
            new MessagingTaskConfigurationArtifact(
                "orders.reserve",
                new SemanticVersion(1, 0, 0),
                null!),
            null!,
            null!,
            OnErrorPolicy.Stop,
            null!,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static TaskExecution CreateFailedTask(Id instanceId)
        => new()
        {
            Id = Id.New(),
            OrchestrationInstanceId = instanceId,
            StageExecutionId = Id.New(),
            TaskKey = "reserve-stock",
            TaskKind = TaskKind.Messaging,
            Status = TaskExecutionStatus.Failed,
            FailedOnUtc = DateTime.UtcNow.AddMinutes(-1)
        };

    private sealed class RecoveryFixture
    {
        public RecoveryFixture()
        {
            InstanceRepository.Update(Arg.Any<OrchestrationInstance>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            StageRepository.Update(Arg.Any<StageExecution>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TransitionRepository
                .Create(Arg.Do<ExecutionTransition>(transition => Transitions.Add(transition)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TaskAttemptDispatcher
                .DispatchAsync(Arg.Do<TaskAttemptDispatchRequest>(request => Dispatches.Add(request)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            DispatchReplayService
                .ReplayAsync(Arg.Any<TaskDispatchReplayRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(TaskDispatchReplayResult.Rejected("Unsupported", "Delivery replay is not configured.")));
            SagaEngine
                .OrchestrateAsync(Arg.Do<ForwardIntent>(intent => Forwards.Add(intent)), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Service = new DefaultOrchestrationRecoveryService(
                InstanceRepository,
                StageRepository,
                TaskRepository,
                TransitionRepository,
                ArtifactResolver,
                TaskAttemptDispatcher,
                SagaEngine,
                AttemptRepository,
                DispatchRepository,
                DispatchReplayService,
                new NoopRuntimeStorageUnitOfWork(),
                DispatchCancellationCoordinator);
        }

        public IOrchestrationInstanceRepository InstanceRepository { get; } =
            Substitute.For<IOrchestrationInstanceRepository>();

        public IStageExecutionRepository StageRepository { get; } =
            Substitute.For<IStageExecutionRepository>();

        public ITaskExecutionRepository TaskRepository { get; } =
            Substitute.For<ITaskExecutionRepository>();

        public IExecutionTransitionRepository TransitionRepository { get; } =
            Substitute.For<IExecutionTransitionRepository>();

        public ITaskExecutionAttemptRepository AttemptRepository { get; } =
            Substitute.For<ITaskExecutionAttemptRepository>();

        public ITaskDispatchRepository DispatchRepository { get; } =
            Substitute.For<ITaskDispatchRepository>();

        public IRuntimeArtifactResolver ArtifactResolver { get; } =
            Substitute.For<IRuntimeArtifactResolver>();

        public ITaskAttemptDispatcher TaskAttemptDispatcher { get; } =
            Substitute.For<ITaskAttemptDispatcher>();

        public ITaskDispatchReplayService DispatchReplayService { get; } =
            Substitute.For<ITaskDispatchReplayService>();

        public IOrchestrationDispatchCancellationCoordinator DispatchCancellationCoordinator { get; } =
            Substitute.For<IOrchestrationDispatchCancellationCoordinator>();

        public ISagaEngine SagaEngine { get; } =
            Substitute.For<ISagaEngine>();

        public DefaultOrchestrationRecoveryService Service { get; }

        public List<ExecutionTransition> Transitions { get; } = [];

        public List<TaskAttemptDispatchRequest> Dispatches { get; } = [];

        public List<ForwardIntent> Forwards { get; } = [];

        public void ArrangeReplay(
            OrchestrationInstance instance,
            ResolvedOrchestrationArtifact resolvedArtifact,
            IReadOnlyCollection<StageExecution> stages,
            IReadOnlyCollection<TaskExecution> tasks)
        {
            InstanceRepository
                .GetById(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(instance));
            ArtifactResolver
                .ResolveAsync(instance.RuntimeOrchestrationArtifactId.ToString(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(resolvedArtifact));
            StageRepository
                .GetByInstanceId(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(stages));
            TaskRepository
                .GetByInstanceId(instance.Id, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(tasks));
        }

        public void ArrangeDispatchReplay(
            OrchestrationInstance instance,
            TaskExecution task,
            TaskExecutionAttempt attempt,
            TaskDispatch dispatch)
        {
            InstanceRepository.GetById(instance.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(instance));
            TaskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(task));
            AttemptRepository.GetById(attempt.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(attempt));
            DispatchRepository.GetById(dispatch.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(dispatch));
        }
    }
}
