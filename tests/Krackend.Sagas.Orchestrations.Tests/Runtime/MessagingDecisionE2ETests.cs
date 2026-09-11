namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Client.Errors;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Tests.Client.Support;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;

public sealed class MessagingDecisionE2ETests
{
    private static readonly SemanticVersion MessagingVersion = new(1, 0, 0);

    [Fact]
    public async Task EngineCompletesConfiguredMessagingStagesWhenCallbacksSucceed()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.one", 1)),
            Stage("stage-two", 2, MessagingTask("task.two", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-1");

        Assert.Single(harness.Dispatcher.Commands);
        var firstCommand = harness.Dispatcher.Commands[0];
        Assert.Equal("task.one", firstCommand.TaskKey);

        await harness.ForwardAsync(firstCommand, BusinessPayload("response-one"), Success("service-a", "operation-a"));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var secondCommand = harness.Dispatcher.Commands[1];
        Assert.Equal("task.two", secondCommand.TaskKey);

        await harness.ForwardAsync(secondCommand, BusinessPayload("response-two"), Success("service-b", "operation-b"));

        var instance = await harness.GetInstanceAsync(firstCommand);
        var firstAttempt = await harness.GetAttemptAsync(firstCommand);
        var transitions = await harness.GetTransitionsAsync(firstCommand);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal("response-one", firstAttempt.ResponsePayload!["value"]!.GetValue<string>());
        Assert.False(firstAttempt.ResponsePayload.AsObject().ContainsKey(nameof(OrchestrationExecutionResultMetadata.Succeeded)));
        Assert.True(firstAttempt.Metadata["ExecutionSucceeded"]!.GetValue<bool>());
        Assert.Equal("operation-a", firstAttempt.Metadata["ExecutionOperationName"]!.GetValue<string>());
        Assert.Contains(transitions, transition => transition.TransitionType == "InstanceCompleted");
    }

    [Fact]
    public async Task EngineRetriesCallbackFailureWhenExecutionErrorCodeIsRetryable()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.retryable",
                1,
                retryPolicy: RetryPolicy(1, "TransientFailure")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-2");

        var firstCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(firstCommand, null, Failure("TransientFailure", "temporary outage"));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var retryCommand = harness.Dispatcher.Commands[1];
        Assert.Equal(2, retryCommand.MessageMetadata.Attempt);

        await harness.ForwardAsync(retryCommand, BusinessPayload("retry-response"), Success());

        var instance = await harness.GetInstanceAsync(firstCommand);
        var attempts = (await harness.GetAttemptsAsync(firstCommand)).OrderBy(x => x.AttemptNumber).ToArray();

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(2, attempts.Length);
        Assert.Equal(TaskExecutionStatus.Failed, attempts[0].Status);
        Assert.Equal("TransientFailure", attempts[0].ErrorCode);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
    }

    [Fact]
    public async Task EngineRetriesWhenClientMapsExceptionToRetryableErrorCode()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.client.retryable",
                1,
                retryPolicy: RetryPolicy(1, "InventoryUnavailable")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-client-retry");

        var firstCommand = harness.Dispatcher.Commands.Single();
        var clientReport = await CreateClientFailureReportAsync<ReserveInventoryRequest>(
            firstCommand.MessageMetadata,
            new TransientClientFailureException("inventory dependency unavailable"),
            options => options.Map<TransientClientFailureException>(
                "InventoryUnavailable",
                isRetryableCandidate: true));

        Assert.Null(clientReport.Payload);
        Assert.Equal("InventoryUnavailable", clientReport.ResultMetadata.ErrorCode);
        Assert.True(clientReport.ResultMetadata.IsRetryableCandidate);

        await harness.ForwardAsync(firstCommand, clientReport.Payload, clientReport.ResultMetadata);

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var retryCommand = harness.Dispatcher.Commands[1];
        Assert.Equal(2, retryCommand.MessageMetadata.Attempt);

        await harness.ForwardAsync(retryCommand, BusinessPayload("retry-response"), Success());

        var instance = await harness.GetInstanceAsync(firstCommand);
        var attempts = (await harness.GetAttemptsAsync(firstCommand)).OrderBy(x => x.AttemptNumber).ToArray();

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, attempts[0].Status);
        Assert.Equal("InventoryUnavailable", attempts[0].ErrorCode);
        Assert.True(attempts[0].Metadata["ExecutionIsRetryableCandidate"]!.GetValue<bool>());
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
    }

    [Fact]
    public async Task EngineStopsWhenFailureCodeIsNotRetryable()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.permanent",
                1,
                retryPolicy: RetryPolicy(2, "TransientFailure")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-3");

        var command = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(command, null, Failure("PermanentFailure", "do not retry"));

        var instance = await harness.GetInstanceAsync(command);
        var task = await harness.GetTaskAsync(command);
        var attempt = await harness.GetAttemptAsync(command);

        Assert.Single(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal("PermanentFailure", attempt.ErrorCode);
    }

    [Fact]
    public async Task EngineStopsAfterRetryBudgetIsExhausted()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.exhausted",
                1,
                retryPolicy: RetryPolicy(1, "TransientFailure")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-4");

        var firstCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(firstCommand, null, Failure("TransientFailure", "first failure"));

        var retryCommand = harness.Dispatcher.Commands[1];
        await harness.ForwardAsync(retryCommand, null, Failure("TransientFailure", "second failure"));

        var instance = await harness.GetInstanceAsync(firstCommand);
        var task = await harness.GetTaskAsync(firstCommand);
        var attempts = await harness.GetAttemptsAsync(firstCommand);

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal(2, attempts.Count);
    }

    [Fact]
    public async Task EngineContinuesAfterNonRetryableFailureWhenTaskPolicyAllowsIt()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage(
                "stage-one",
                1,
                MessagingTask("task.optional", 1, onErrorPolicy: OnErrorPolicy.Continue),
                MessagingTask("task.required", 2))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-5");

        var optionalCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(optionalCommand, null, Failure("IgnoredFailure", "optional task failed"));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var requiredCommand = harness.Dispatcher.Commands[1];
        Assert.Equal("task.required", requiredCommand.TaskKey);

        await harness.ForwardAsync(requiredCommand, BusinessPayload("required-response"), Success());

        var instance = await harness.GetInstanceAsync(optionalCommand);
        var optionalAttempt = await harness.GetAttemptAsync(optionalCommand);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, optionalAttempt.Status);
        Assert.Equal("IgnoredFailure", optionalAttempt.ErrorCode);
    }

    [Fact]
    public async Task EngineCompensatesCompletedTasksWhenStopAndCompensateTaskFails()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.completed",
                1,
                compensation: Compensation("task.completed.undo"))),
            Stage("stage-two", 2, MessagingTask(
                "task.failing",
                1,
                onErrorPolicy: OnErrorPolicy.StopAndCompensate))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-6");

        var completedCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(completedCommand, BusinessPayload("completed-response"), Success());

        var failingCommand = harness.Dispatcher.Commands[1];
        await harness.ForwardAsync(failingCommand, null, Failure("PermanentFailure", "requires compensation"));

        var instance = await harness.GetInstanceAsync(completedCommand);
        var compensationCommand = harness.Dispatcher.Commands[2];

        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Equal("task.completed", compensationCommand.TaskKey);
        Assert.False(compensationCommand.AwaitResponse);
        Assert.Contains("task.completed.undo", compensationCommand.SettingsPayload);
    }

    [Fact]
    public async Task EngineCompensatesWhenClientMapsExceptionToPermanentErrorCode()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.completed",
                1,
                compensation: Compensation("task.completed.undo"))),
            Stage("stage-two", 2, MessagingTask(
                "task.client.permanent",
                1,
                retryPolicy: RetryPolicy(2, "TransientFailure"),
                onErrorPolicy: OnErrorPolicy.StopAndCompensate))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-client-compensation");

        var completedCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(completedCommand, BusinessPayload("completed-response"), Success());

        var failingCommand = harness.Dispatcher.Commands[1];
        var clientReport = await CreateClientFailureReportAsync<CapturePaymentRequest>(
            failingCommand.MessageMetadata,
            new PermanentClientFailureException("payment rejected"),
            options => options.Map<PermanentClientFailureException>(
                "PaymentRejected",
                isRetryableCandidate: false));

        await harness.ForwardAsync(failingCommand, clientReport.Payload, clientReport.ResultMetadata);

        var instance = await harness.GetInstanceAsync(completedCommand);
        var failedAttempt = await harness.GetAttemptAsync(failingCommand);
        var compensationCommand = harness.Dispatcher.Commands[2];

        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Equal("PaymentRejected", failedAttempt.ErrorCode);
        Assert.False(failedAttempt.Metadata["ExecutionIsRetryableCandidate"]!.GetValue<bool>());
        Assert.Equal("task.completed", compensationCommand.TaskKey);
        Assert.False(compensationCommand.AwaitResponse);
    }

    [Fact]
    public async Task EngineIgnoresDuplicateCallbackAfterTaskAlreadyCompleted()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.once", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-7");

        var command = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(command, BusinessPayload("first-response"), Success());
        await harness.ForwardAsync(command, BusinessPayload("duplicate-response"), Failure("LateFailure", "duplicate"));

        var instance = await harness.GetInstanceAsync(command);
        var attempt = await harness.GetAttemptAsync(command);
        var transitions = await harness.GetTransitionsAsync(command);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal("first-response", attempt.ResponsePayload!["value"]!.GetValue<string>());
        Assert.Equal(1, transitions.Count(transition => transition.TransitionType == "TaskCallbackCompleted"));
        Assert.DoesNotContain(transitions, transition => transition.TransitionType == "TaskCallbackFailed");
    }

    [Fact]
    public async Task EngineIgnoresCallbackWhenMetadataDoesNotMatchCurrentRuntimeState()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.current", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-invalid-callback");

        var command = harness.Dispatcher.Commands.Single();
        command.MessageMetadata.OrchestrationInstanceId = Id.New().ToString();
        await harness.ForwardAsync(command, BusinessPayload("invalid-callback"), Success());

        var task = await harness.GetTaskAsync(command);
        var attempt = await harness.GetAttemptAsync(command);
        var transitions = await harness.GetTransitionsAsync(command);

        Assert.Equal(TaskExecutionStatus.WaitingResponse, task.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, attempt.Status);
        Assert.DoesNotContain(transitions, transition => transition.TransitionType == "TaskCallbackCompleted");
    }

    [Fact]
    public async Task EngineRetriesDispatchFailureUsingPersistedAttemptErrorCode()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.dispatch",
                1,
                retryPolicy: RetryPolicy(1, "CommandDispatchFailed")))));
        harness.Dispatcher.FailNext(new TimeoutException("transport unavailable"));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-8");

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var retryCommand = harness.Dispatcher.Commands[1];
        Assert.Equal(2, retryCommand.MessageMetadata.Attempt);

        await harness.ForwardAsync(retryCommand, BusinessPayload("dispatch-response"), Success());

        var instance = await harness.GetInstanceAsync(retryCommand);
        var attempts = (await harness.GetAttemptsAsync(retryCommand)).OrderBy(x => x.AttemptNumber).ToArray();

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal("CommandDispatchFailed", attempts[0].ErrorCode);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
    }

    [Fact]
    public async Task EngineRetriesPreparationFailureUsingPersistedAttemptErrorCode()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(
                Stage("stage-one", 1, MessagingTask(
                    "task.prepare",
                    1,
                    retryPolicy: RetryPolicy(1, "PreparationTransient")))),
            services =>
            {
                services.AddScoped<SequencedTaskDispatchRequestPayloadPreparer>();
                services.Replace(ServiceDescriptor.Scoped<ITaskDispatchRequestPayloadPreparer>(provider =>
                    provider.GetRequiredService<SequencedTaskDispatchRequestPayloadPreparer>()));
            });
        var preparer = harness.GetRequiredService<SequencedTaskDispatchRequestPayloadPreparer>();
        preparer.FailNext("PreparationTransient", "preparation dependency unavailable");

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-9");

        Assert.Single(harness.Dispatcher.Commands);
        var retryCommand = harness.Dispatcher.Commands.Single();
        Assert.Equal(2, retryCommand.MessageMetadata.Attempt);

        await harness.ForwardAsync(retryCommand, BusinessPayload("prepared-response"), Success());

        var instance = await harness.GetInstanceAsync(retryCommand);
        var attempts = (await harness.GetAttemptsAsync(retryCommand)).OrderBy(x => x.AttemptNumber).ToArray();

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal("PreparationTransient", attempts[0].ErrorCode);
        Assert.Equal(2, preparer.Calls);
    }

    [Fact]
    public async Task EngineFailsCallbackWithoutExecutionResultMetadata()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.metadata", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-10");

        var command = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(command, BusinessPayload("untrusted-response"), null);

        var instance = await harness.GetInstanceAsync(command);
        var attempt = await harness.GetAttemptAsync(command);

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal("MissingExecutionResultMetadata", attempt.ErrorCode);
        Assert.Equal("untrusted-response", attempt.ResponsePayload!["value"]!.GetValue<string>());
        Assert.False(attempt.ResponsePayload.AsObject().ContainsKey(nameof(OrchestrationExecutionResultMetadata.Status)));
    }

    [Fact]
    public async Task EngineCompletesFireAndForgetTaskWithoutCallback()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.fire-and-forget",
                1,
                dispatchType: TaskDispatchType.FireAndForget))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-11");

        var command = harness.Dispatcher.Commands.Single();
        var instance = await harness.GetInstanceAsync(command);
        var task = await harness.GetTaskAsync(command);

        Assert.False(command.AwaitResponse);
        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Completed, task.Status);
    }

    [Fact]
    public async Task TimeoutProcessorLeavesWaitingTaskUntilTimeoutIsDue()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.waiting",
                1,
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(60), "TIMEOUT")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-12");

        var command = harness.Dispatcher.Commands.Single();
        var task = await harness.GetTaskAsync(command);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        var processed = await processor.ProcessDueTimeoutsAsync(task.WaitingSinceUtc!.Value.AddSeconds(59));
        task = await harness.GetTaskAsync(command);

        Assert.Equal(0, processed);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, task.Status);
        Assert.Single(harness.Dispatcher.Commands);
    }

    [Fact]
    public async Task TimeoutProcessorRetriesTimedOutTaskWhenTimeoutErrorCodeIsRetryable()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.timeout.retry",
                1,
                retryPolicy: RetryPolicy(1, "TIMEOUT"),
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(1), "TIMEOUT")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-13");

        var firstCommand = harness.Dispatcher.Commands.Single();
        var firstTask = await harness.GetTaskAsync(firstCommand);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        var processed = await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(2));

        Assert.Equal(1, processed);
        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var retryCommand = harness.Dispatcher.Commands[1];
        Assert.Equal(2, retryCommand.MessageMetadata.Attempt);

        await harness.ForwardAsync(retryCommand, BusinessPayload("timeout-retry-response"), Success());

        var instance = await harness.GetInstanceAsync(firstCommand);
        var attempts = (await harness.GetAttemptsAsync(firstCommand)).OrderBy(x => x.AttemptNumber).ToArray();
        var transitions = await harness.GetTransitionsAsync(firstCommand);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(TaskExecutionStatus.TimedOut, attempts[0].Status);
        Assert.Equal("TIMEOUT", attempts[0].ErrorCode);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskTimedOut");
    }

    [Fact]
    public async Task TimeoutProcessorContinuesAfterTimedOutTaskWhenTaskPolicyAllowsIt()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage(
                "stage-one",
                1,
                MessagingTask(
                    "task.timeout.optional",
                    1,
                    onErrorPolicy: OnErrorPolicy.Continue,
                    timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(1), "TIMEOUT")),
                MessagingTask("task.after-timeout", 2))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-14");

        var firstCommand = harness.Dispatcher.Commands.Single();
        var firstTask = await harness.GetTaskAsync(firstCommand);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(2));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var secondCommand = harness.Dispatcher.Commands[1];
        Assert.Equal("task.after-timeout", secondCommand.TaskKey);

        await harness.ForwardAsync(secondCommand, BusinessPayload("after-timeout-response"), Success());

        var instance = await harness.GetInstanceAsync(firstCommand);
        var timedOutAttempt = await harness.GetAttemptAsync(firstCommand);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(TaskExecutionStatus.TimedOut, timedOutAttempt.Status);
    }

    [Fact]
    public async Task TimeoutProcessorCompensatesCompletedTasksWhenTimedOutTaskRequiresCompensation()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.completed",
                1,
                compensation: Compensation("task.completed.undo"))),
            Stage("stage-two", 2, MessagingTask(
                "task.timeout.compensate",
                1,
                onErrorPolicy: OnErrorPolicy.StopAndCompensate,
                timeoutPolicy: TimeoutPolicy(Duration.FromSeconds(1), "TIMEOUT")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-15");

        var completedCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(completedCommand, BusinessPayload("completed-response"), Success());

        var waitingCommand = harness.Dispatcher.Commands[1];
        var waitingTask = await harness.GetTaskAsync(waitingCommand);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        await processor.ProcessDueTimeoutsAsync(waitingTask.WaitingSinceUtc!.Value.AddSeconds(2));

        var instance = await harness.GetInstanceAsync(completedCommand);
        var compensationCommand = harness.Dispatcher.Commands[2];

        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Equal("task.completed", compensationCommand.TaskKey);
        Assert.Contains("task.completed.undo", compensationCommand.SettingsPayload);
    }

    [Fact]
    public async Task TimeoutProcessorAppliesWaitGraceBeforeContinueDecision()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage(
                "stage-one",
                1,
                MessagingTask(
                    "task.timeout.wait",
                    1,
                    timeoutPolicy: WaitTimeoutPolicy(
                        Duration.FromSeconds(1),
                        Duration.FromSeconds(10),
                        OrchestrationActionOnTimeout.Continue)),
                MessagingTask("task.after-wait", 2))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-16");

        var firstCommand = harness.Dispatcher.Commands.Single();
        var firstTask = await harness.GetTaskAsync(firstCommand);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        var firstProcessed = await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(2));
        var stillWaiting = await harness.GetTaskAsync(firstCommand);
        var secondProcessed = await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(5));

        Assert.Equal(1, firstProcessed);
        Assert.Equal(0, secondProcessed);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, stillWaiting.Status);
        Assert.Single(harness.Dispatcher.Commands);

        await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(13));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        Assert.Equal("task.after-wait", harness.Dispatcher.Commands[1].TaskKey);
    }

    [Fact]
    public async Task TimeoutProcessorUsesReconcileRetryPolicyForTimedOutTask()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.timeout.reconcile",
                1,
                timeoutPolicy: ReconcileTimeoutPolicy(
                    Duration.FromSeconds(1),
                    RetryPolicy(1, "TaskTimedOut"))))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-17");

        var firstCommand = harness.Dispatcher.Commands.Single();
        var firstTask = await harness.GetTaskAsync(firstCommand);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(2));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        Assert.Equal(2, harness.Dispatcher.Commands[1].MessageMetadata.Attempt);
    }

    private static OrchestrationArtifact CreateArtifact(params StageArtifact[] stages)
        => new(
            Id.New(),
            Id.New(),
            "test.orchestration",
            "Test Orchestration",
            "test",
            MessagingVersion,
            new Checksum($"messaging-decision-e2e-{Guid.NewGuid():N}"),
            [],
            [],
            stages);

    private static StageArtifact Stage(string key, int order, params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, ExecutionCondition(), tasks, [], []);

    private static TaskArtifact MessagingTask(
        string key,
        int order,
        RetryPolicyArtifact? retryPolicy = null,
        TimeoutPolicyArtifact? timeoutPolicy = null,
        OnErrorPolicy onErrorPolicy = OnErrorPolicy.Stop,
        CompensationArtifact? compensation = null,
        TaskDispatchType dispatchType = TaskDispatchType.FireAndWait)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            ExecutionCondition(),
            Transformation(),
            new MessagingTaskConfigurationArtifact(key, MessagingVersion, null!),
            retryPolicy,
            timeoutPolicy,
            onErrorPolicy,
            compensation,
            dispatchType,
            true);

    private static TransformationArtifact Transformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact());

    private static ExecutionConditionArtifact ExecutionCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true")));

    private static RetryPolicyArtifact RetryPolicy(int maxRetries, params string[] retryableErrorCodes)
        => new(
            maxRetries,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            retryableErrorCodes,
            true);

    private static TimeoutPolicyArtifact TimeoutPolicy(Duration timeout, string errorCode)
        => new(timeout, TimeoutBehavior.Fail, new FailTimeoutBehaviorPolicyArtifact(errorCode));

    private static TimeoutPolicyArtifact WaitTimeoutPolicy(
        Duration timeout,
        Duration waitingTime,
        OrchestrationActionOnTimeout orchestrationAction)
        => new(
            timeout,
            TimeoutBehavior.Wait,
            new WaitTimeoutBehaviorPolicyArtifact(orchestrationAction, waitingTime));

    private static TimeoutPolicyArtifact ReconcileTimeoutPolicy(Duration timeout, RetryPolicyArtifact retryPolicy)
        => new(
            timeout,
            TimeoutBehavior.Reconcile,
            new ReconcileTimeoutBehaviorPolicyArtifact(OrchestrationActionOnTimeout.Block, retryPolicy));

    private static CompensationArtifact Compensation(string topic)
        => new(
            TaskKind.Messaging,
            Transformation(),
            ExecutionCondition(),
            new MessagingTaskConfigurationArtifact(topic, MessagingVersion, null!),
            null,
            null,
            TaskDispatchType.FireAndForget);

    private static OrchestrationExecutionResultMetadata Success(
        string? serviceName = null,
        string? operationName = null)
        => new()
        {
            Succeeded = true,
            Status = "Succeeded",
            ServiceName = serviceName,
            OperationName = operationName,
            CompletedOnUtc = DateTime.UtcNow
        };

    private static OrchestrationExecutionResultMetadata Failure(
        string errorCode,
        string errorMessage)
        => new()
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CompletedOnUtc = DateTime.UtcNow
        };

    private static JsonNode BusinessPayload(string value)
        => JsonNode.Parse($$"""{"value":"{{value}}"}""")!;

    private static async Task<ClientFailureReport> CreateClientFailureReportAsync<TRequest>(
        OrchestrationMessageMetadata messageMetadata,
        Exception exception,
        Action<OrchestrationClientErrorMappingOptions> configureErrorMapping)
    {
        var services = new ServiceCollection();
        services.AddKrackendOrchestrationsClient(configureErrorMapping);
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationClientPublisher, RecordingOrchestrationClientPublisher>());

        using var scope = services.BuildServiceProvider().CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IOrchestrationMessageMetadataSetter>()
            .Set(messageMetadata);

        var client = scope.ServiceProvider.GetRequiredService<IOrchestrationOperationClient>();
        client.Begin<TRequest>();

        await client.ReportFailureAsync<TRequest>(exception);

        client.Close();

        var publisher = (RecordingOrchestrationClientPublisher)scope.ServiceProvider.GetRequiredService<IOrchestrationClientPublisher>();
        var payload = publisher.Payload is JsonNode jsonNode ? jsonNode : null;
        return new ClientFailureReport(payload, publisher.ResultMetadata!);
    }

    private sealed record ClientFailureReport(
        JsonNode? Payload,
        OrchestrationExecutionResultMetadata ResultMetadata);

    private sealed record ReserveInventoryRequest;

    private sealed record CapturePaymentRequest;

    private sealed class TransientClientFailureException : Exception
    {
        public TransientClientFailureException(string message)
            : base(message)
        {
        }
    }

    private sealed class PermanentClientFailureException : Exception
    {
        public PermanentClientFailureException(string message)
            : base(message)
        {
        }
    }
}
