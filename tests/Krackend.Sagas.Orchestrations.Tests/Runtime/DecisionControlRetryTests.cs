namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class DecisionControlRetryTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DecideAsyncReturnsRetryWhenExecutionErrorCodeIsRetryable()
    {
        var state = await CreateRetryStateAsync();
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(new DecisionRequest
        {
            ArtifactId = state.ArtifactId.ToString(),
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = state.InstanceId.ToString()
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                ErrorCode = "TemporaryInventoryFailure"
            },
            Payload = JsonNode.Parse("""{"ignored":"business payload should not decide retry"}""")
        }, CancellationToken.None);

        Assert.Contains(decisions, decision => decision.Kind == "retry-task");
    }

    [Fact]
    public async Task DecideAsyncDoesNotRetryWhenExecutionErrorCodeIsNotRetryable()
    {
        var state = await CreateRetryStateAsync();
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(new DecisionRequest
        {
            ArtifactId = state.ArtifactId.ToString(),
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = state.InstanceId.ToString()
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                ErrorCode = "PermanentInventoryFailure"
            },
            Payload = JsonNode.Parse("""{"errorCode":"TemporaryInventoryFailure"}""")
        }, CancellationToken.None);

        Assert.DoesNotContain(decisions, decision => decision.Kind == "retry-task");
    }

    [Theory]
    [InlineData(OrchestrationInstanceStatus.Completed)]
    [InlineData(OrchestrationInstanceStatus.CompletedWithErrors)]
    [InlineData(OrchestrationInstanceStatus.Stopped)]
    [InlineData(OrchestrationInstanceStatus.Compensating)]
    [InlineData(OrchestrationInstanceStatus.Compensated)]
    [InlineData(OrchestrationInstanceStatus.Aborted)]
    [InlineData(OrchestrationInstanceStatus.DeadLettered)]
    public async Task DecideAsyncReturnsNoDecisionsForTerminalOrDeadLetteredInstances(OrchestrationInstanceStatus status)
    {
        var state = await CreateRetryStateAsync();
        await UpdateInstanceAsync(state, instance => instance.Status = status);
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state), CancellationToken.None);

        Assert.Empty(decisions);
    }

    [Fact]
    public async Task DecideAsyncDeadLettersFailedInstanceWithoutRecoverableTask()
    {
        var state = await CreateRetryStateAsync();
        await UpdateInstanceAsync(state, instance =>
        {
            instance.Status = OrchestrationInstanceStatus.Failed;
            instance.ErrorSummary = null;
        });
        await UpdateTaskAsync(state, task => task.Status = TaskExecutionStatus.Completed);
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state), CancellationToken.None);

        var decision = Assert.Single(decisions);
        Assert.Equal("dead-letter-instance", decision.Kind);
    }

    [Fact]
    public async Task DecideAsyncStopsWhenCompensationAlreadyFailedTerminally()
    {
        var state = await CreateRetryStateAsync();
        await UpdateInstanceAsync(state, instance =>
            instance.Metadata["Compensation.TerminalFailure"] = JsonValue.Create(true)!);
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state, "TemporaryInventoryFailure"), CancellationToken.None);

        Assert.Empty(decisions);
    }

    [Fact]
    public async Task DecideAsyncCompensatesWhenFailedTaskPolicyRequiresCompensation()
    {
        var state = await CreateRetryStateAsync(task => task with
        {
            OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
            RetryPolicy = null!
        });
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state), CancellationToken.None);

        var decision = Assert.Single(decisions);
        Assert.Equal("compensate-instance", decision.Kind);
    }

    [Fact]
    public async Task DecideAsyncContinuesAfterFailureWhenTaskPolicyAllowsIt()
    {
        var state = await CreateRetryStateAsync(task => task with
        {
            OnErrorPolicy = OnErrorPolicy.Continue,
            RetryPolicy = null!
        });
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state), CancellationToken.None);

        var decision = Assert.Single(decisions);
        Assert.Equal("complete-stage", decision.Kind);
    }

    [Fact]
    public async Task DecideAsyncContinuesAfterTimedOutTaskWhenWaitPolicyAllowsIt()
    {
        var state = await CreateRetryStateAsync(task => task with
        {
            RetryPolicy = null!,
            TimeoutPolicy = new TimeoutPolicyArtifact(
                Duration.FromSeconds(30),
                TimeoutBehavior.Wait,
                new WaitTimeoutBehaviorPolicyArtifact(
                    OrchestrationActionOnTimeout.Continue,
                    Duration.FromSeconds(10)))
        });
        await UpdateTaskAsync(state, task => task.Status = TaskExecutionStatus.TimedOut);
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state), CancellationToken.None);

        var decision = Assert.Single(decisions);
        Assert.Equal("complete-stage", decision.Kind);
    }

    [Fact]
    public async Task DecideAsyncDoesNotRetryWhenRetryIsSuppressedOrCandidateIsNonRetryable()
    {
        var suppressedState = await CreateRetryStateAsync();
        await UpdateTaskAsync(suppressedState, task =>
            task.Metadata["RetrySuppressed"] = JsonValue.Create(true)!);
        var suppressedDecisionControl = suppressedState.Services.GetRequiredService<IDecisionControl>();

        var suppressed = await suppressedDecisionControl.DecideAsync(
            Request(suppressedState, "TemporaryInventoryFailure"),
            CancellationToken.None);

        var nonRetryableState = await CreateRetryStateAsync();
        var nonRetryableDecisionControl = nonRetryableState.Services.GetRequiredService<IDecisionControl>();

        var nonRetryable = await nonRetryableDecisionControl.DecideAsync(
            Request(nonRetryableState, "TemporaryInventoryFailure", isRetryableCandidate: false),
            CancellationToken.None);

        Assert.DoesNotContain(suppressed, decision => decision.Kind == "retry-task");
        Assert.DoesNotContain(nonRetryable, decision => decision.Kind == "retry-task");
    }

    [Fact]
    public async Task DecideAsyncCanResolveRetryableErrorCodeFromAttemptMetadata()
    {
        var state = await CreateRetryStateAsync();
        await AddAttemptAsync(state, attempt =>
        {
            attempt.AttemptNumber = 2;
            attempt.Metadata["ExecutionErrorCode"] = JsonValue.Create("TemporaryInventoryFailure")!;
        });
        var decisionControl = state.Services.GetRequiredService<IDecisionControl>();

        var decisions = await decisionControl.DecideAsync(Request(state), CancellationToken.None);

        Assert.Contains(decisions, decision => decision.Kind == "retry-task");
    }

    [Fact]
    public void PrivateMetadataHelpersCoverRetryDeadLetterAndBooleanBranches()
    {
        var metadata = new Dictionary<string, JsonNode>
        {
            ["trueValue"] = JsonValue.Create(true)!,
            ["falseValue"] = JsonValue.Create(false)!,
            ["stringTrue"] = JsonValue.Create("true")!,
            ["stringFalse"] = JsonValue.Create("false")!,
            ["invalidString"] = JsonValue.Create("maybe")!,
            ["numberValue"] = JsonValue.Create(7)!,
            ["objectValue"] = JsonNode.Parse("""{"code":"A"}""")!
        };
        var retrySuppressedTask = CreateTaskExecution();
        retrySuppressedTask.Metadata["RetrySuppressed"] = JsonValue.Create(true)!;
        var terminalFailureInstance = CreateInstance();
        terminalFailureInstance.Metadata["Compensation.TerminalFailure"] = JsonValue.Create(true)!;
        var failedTask = CreateTaskExecution(taskKey: "inventories.reserve");

        Assert.True(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", metadata, "trueValue"));
        Assert.False(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", metadata, "falseValue"));
        Assert.True(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", metadata, "stringTrue"));
        Assert.False(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", metadata, "stringFalse"));
        Assert.Null(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", metadata, "invalidString"));
        Assert.Null(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", metadata, "numberValue"));
        Assert.Null(InvokeDecisionControlPrivateStatic<bool?>("TryGetNullableBoolean", null!, "missing"));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("TryGetBoolean", metadata, "trueValue"));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("TryGetBoolean", metadata, "missing"));
        Assert.Equal("""{"code":"A"}""", InvokeDecisionControlPrivateStatic<string>("TryGetString", metadata, "objectValue"));
        Assert.Equal("true", InvokeDecisionControlPrivateStatic<string>("TryGetString", metadata, "stringTrue"));
        Assert.Null(InvokeDecisionControlPrivateStatic<string>("TryGetString", metadata, "missing"));
        Assert.Null(InvokeDecisionControlPrivateStatic<string>("TryGetString", null!, "missing"));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("ShouldSuppressRetry", retrySuppressedTask));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("ShouldSuppressRetry", null!));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("HasCompensationTerminalFailure", terminalFailureInstance));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("HasCompensationTerminalFailure", null!));

        var summarized = CreateInstance();
        summarized.ErrorSummary = "Domain error.";
        Assert.Equal("Domain error.", InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", summarized, failedTask));
        summarized.ErrorSummary = " ";

        failedTask.Metadata["ExecutionErrorCode"] = JsonValue.Create("InventoryUnavailable")!;
        var codedReason = InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", CreateInstance(), failedTask);
        Assert.Contains("InventoryUnavailable", codedReason, StringComparison.Ordinal);

        failedTask.Metadata.Clear();
        failedTask.Metadata["ExecutionErrorCode"] = JsonValue.Create(" ")!;
        var blankErrorCodeReason = InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", summarized, failedTask);
        Assert.Contains("no retry, continue, or compensation policy", blankErrorCodeReason, StringComparison.Ordinal);

        failedTask.Metadata.Clear();
        failedTask.Metadata["PreparationErrorCode"] = JsonValue.Create("PreparationFailed")!;
        var preparationReason = InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", CreateInstance(), failedTask);
        Assert.Contains("PreparationFailed", preparationReason, StringComparison.Ordinal);

        failedTask.Metadata.Clear();
        failedTask.Metadata["TimeoutErrorCode"] = JsonValue.Create("TaskTimedOut")!;
        var timeoutReason = InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", CreateInstance(), failedTask);
        Assert.Contains("TaskTimedOut", timeoutReason, StringComparison.Ordinal);

        failedTask.Metadata.Clear();
        var fallbackReason = InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", CreateInstance(), failedTask);
        Assert.Contains("no retry, continue, or compensation policy", fallbackReason, StringComparison.Ordinal);
        var missingTaskReason = InvokeDecisionControlPrivateStatic<string>("ResolveDeadLetterReason", CreateInstance(), null!);
        Assert.Contains("Task '' failed", missingTaskReason, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateStatusHelpersCoverCallbackRuntimeAndTerminalBranches()
    {
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsCallbackSignal", null!));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsCallbackSignal", new OrchestrationMessageMetadata()));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsCallbackSignal", new OrchestrationMessageMetadata { TaskExecutionId = "task-1" }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsCallbackSignal", new OrchestrationMessageMetadata { DispatchId = "dispatch-1" }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsRuntimeTimeoutSignal",
            new DecisionRequest { ExecutionResultMetadata = new OrchestrationExecutionResultMetadata { ErrorType = "timeout" } }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsRuntimeTimeoutSignal",
            new DecisionRequest { ExecutionResultMetadata = new OrchestrationExecutionResultMetadata { Status = "TimedOut" } }));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsRuntimeTimeoutSignal", new DecisionRequest()));

        foreach (var status in new[]
        {
            TaskExecutionStatus.Skipped,
            TaskExecutionStatus.Completed,
            TaskExecutionStatus.CompletedWithErrors,
            TaskExecutionStatus.Failed,
            TaskExecutionStatus.TimedOut,
            TaskExecutionStatus.Cancelled,
            TaskExecutionStatus.Compensated
        })
        {
            Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsTerminalTaskStatus", status));
        }

        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsTerminalTaskStatus", TaskExecutionStatus.Pending));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsTerminalTaskStatus", TaskExecutionStatus.Retrying));

        foreach (var status in new[] { "Acknowledged", "Failed", "Completed", "TimedOut" })
        {
            Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsFinishedDispatchStatus", status.ToLowerInvariant()));
        }

        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsFinishedDispatchStatus", "WaitingResponse"));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsStageFinished", StageExecutionStatus.Completed));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsStageFinished", StageExecutionStatus.Skipped));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsStageFinished", StageExecutionStatus.Failed));

        foreach (var status in new[]
        {
            OrchestrationInstanceStatus.Completed,
            OrchestrationInstanceStatus.CompletedWithErrors,
            OrchestrationInstanceStatus.Stopped,
            OrchestrationInstanceStatus.Compensating,
            OrchestrationInstanceStatus.Compensated,
            OrchestrationInstanceStatus.Aborted
        })
        {
            Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsInstanceTerminalForCallback", status));
        }

        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsInstanceTerminalForCallback", OrchestrationInstanceStatus.DeadLettered));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsSuccessfulCallback",
            new OrchestrationExecutionResultMetadata { Succeeded = true }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsSuccessfulCallback",
            new OrchestrationExecutionResultMetadata { Status = "completed" }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsSuccessfulCallback",
            new OrchestrationExecutionResultMetadata { Status = "succeeded" }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsSuccessfulCallback",
            new OrchestrationExecutionResultMetadata { Status = "success" }));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "IsSuccessfulCallback",
            new OrchestrationExecutionResultMetadata { Status = "failed" }));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsSuccessfulCallback", null!));
    }

    [Fact]
    public void PrivateCallbackHelpersCoverReadyResolvedAndLateSuccessBranches()
    {
        var waitingTask = CreateTaskExecution(TaskExecutionStatus.WaitingResponse);
        var waitingAttempt = new TaskExecutionAttempt { Status = TaskExecutionStatus.WaitingResponse };
        var waitingDispatch = new TaskDispatch { DispatchStatus = "waitingresponse", DispatchType = "messaging" };
        var completedTask = CreateTaskExecution(TaskExecutionStatus.Completed);
        var failedAttempt = new TaskExecutionAttempt { Status = TaskExecutionStatus.Failed };
        var completedDispatch = new TaskDispatch { DispatchStatus = "Completed", DispatchType = "messaging" };
        var successfulResult = new OrchestrationExecutionResultMetadata { Succeeded = true };

        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsCallbackReady", waitingTask, waitingAttempt, waitingDispatch));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "IsCallbackReady",
            CreateTaskExecution(TaskExecutionStatus.Running),
            waitingAttempt,
            waitingDispatch));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>("IsCallbackResolved", waitingTask, waitingAttempt, waitingDispatch));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsCallbackResolved", completedTask, waitingAttempt, waitingDispatch));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsCallbackResolved", waitingTask, failedAttempt, waitingDispatch));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>("IsCallbackResolved", waitingTask, waitingAttempt, completedDispatch));

        foreach (var status in new[]
        {
            OrchestrationInstanceStatus.DeadLettered,
            OrchestrationInstanceStatus.Failed,
            OrchestrationInstanceStatus.Running,
            OrchestrationInstanceStatus.Waiting
        })
        {
            Assert.True(InvokeDecisionControlPrivateStatic<bool>(
                "IsRecoverableLateSuccess",
                CreateInstance(status),
                waitingTask,
                waitingAttempt,
                waitingDispatch,
                successfulResult));
        }

        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "IsRecoverableLateSuccess",
            CreateInstance(OrchestrationInstanceStatus.Failed),
            CreateTaskExecution(TaskExecutionStatus.TimedOut),
            new TaskExecutionAttempt { Status = TaskExecutionStatus.Running },
            completedDispatch,
            successfulResult));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "IsRecoverableLateSuccess",
            CreateInstance(OrchestrationInstanceStatus.Failed),
            completedTask,
            waitingAttempt,
            waitingDispatch,
            successfulResult));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "IsRecoverableLateSuccess",
            CreateInstance(OrchestrationInstanceStatus.Failed),
            CreateTaskExecution(TaskExecutionStatus.Compensated),
            waitingAttempt,
            waitingDispatch,
            successfulResult));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "IsRecoverableLateSuccess",
            CreateInstance(OrchestrationInstanceStatus.Created),
            waitingTask,
            waitingAttempt,
            waitingDispatch,
            successfulResult));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "IsRecoverableLateSuccess",
            CreateInstance(OrchestrationInstanceStatus.Failed),
            waitingTask,
            waitingAttempt,
            waitingDispatch,
            new OrchestrationExecutionResultMetadata { Succeeded = false }));
    }

    [Fact]
    public void PrivateBranchAndTaskPlanningHelpersCoverParallelAndBranchTargetBranches()
    {
        var artifact = CreateArtifact();
        var baseStage = artifact.StageDefinitions.Single();
        var baseTask = baseStage.TaskDefinitions.Single();
        var parallelGroupId = Id.New();
        var parallelTasks = new[]
        {
            baseTask with { Key = "inventories.reserve", Name = "Reserve inventory", Order = 1, ExecutionMode = TaskExecutionMode.Parallel, ParallelGroupId = parallelGroupId },
            baseTask with { Id = Id.New(), Key = "payments.capture", Name = "Capture payment", Order = 2, ExecutionMode = TaskExecutionMode.Parallel, ParallelGroupId = parallelGroupId }
        };
        var groupedStage = baseStage with
        {
            TaskDefinitions = parallelTasks,
            ParallelGroups = [new ParallelGroupArtifact(parallelGroupId, ParallelJoinPolicy.WaitAll, 1)]
        };
        var zeroMaxStage = groupedStage with
        {
            ParallelGroups = [new ParallelGroupArtifact(parallelGroupId, ParallelJoinPolicy.WaitAll, 0)]
        };
        var missingGroupStage = groupedStage with
        {
            ParallelGroups = []
        };
        var instance = CreateInstance(OrchestrationInstanceStatus.Running);
        var stageExecution = new StageExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageKey = groupedStage.Key,
            Status = StageExecutionStatus.Running
        };
        var payload = JsonNode.Parse("""{"saleId":"sale-1"}""")!;

        var limitedGroup = InvokeDecisionControlPrivateStatic<IReadOnlyCollection<IDecision>>(
            "BuildNextTaskDecisions",
            instance,
            new[] { groupedStage },
            groupedStage,
            stageExecution,
            groupedStage.TaskDefinitions,
            Array.Empty<TaskExecution>(),
            payload,
            Array.Empty<MetadataDescriptorArtifact>(),
            artifact.ExecutionPolicy)!;
        var missingGroup = InvokeDecisionControlPrivateStatic<IReadOnlyCollection<IDecision>>(
            "BuildNextTaskDecisions",
            instance,
            new[] { missingGroupStage },
            missingGroupStage,
            stageExecution,
            missingGroupStage.TaskDefinitions,
            Array.Empty<TaskExecution>(),
            payload,
            Array.Empty<MetadataDescriptorArtifact>(),
            artifact.ExecutionPolicy)!;
        var zeroGroup = InvokeDecisionControlPrivateStatic<IReadOnlyCollection<IDecision>>(
            "BuildNextTaskDecisions",
            instance,
            new[] { zeroMaxStage },
            zeroMaxStage,
            stageExecution,
            zeroMaxStage.TaskDefinitions,
            Array.Empty<TaskExecution>(),
            payload,
            Array.Empty<MetadataDescriptorArtifact>(),
            artifact.ExecutionPolicy)!;
        var sequentialWithoutPayload = InvokeDecisionControlPrivateStatic<IReadOnlyCollection<IDecision>>(
            "BuildNextTaskDecisions",
            instance,
            new[] { baseStage },
            baseStage,
            stageExecution,
            baseStage.TaskDefinitions,
            Array.Empty<TaskExecution>(),
            null!,
            Array.Empty<MetadataDescriptorArtifact>(),
            artifact.ExecutionPolicy)!;
        var sequential = InvokeDecisionControlPrivateStatic<IReadOnlyCollection<IDecision>>(
            "BuildNextTaskDecisions",
            instance,
            new[] { baseStage },
            baseStage,
            stageExecution,
            baseStage.TaskDefinitions,
            Array.Empty<TaskExecution>(),
            payload,
            Array.Empty<MetadataDescriptorArtifact>(),
            artifact.ExecutionPolicy)!;

        Assert.Single(limitedGroup);
        Assert.Equal(2, missingGroup.Count);
        Assert.Single(zeroGroup);
        Assert.Single(sequential);
        Assert.Single(sequentialWithoutPayload);
        Assert.All(limitedGroup.Concat(missingGroup).Concat(sequential).Concat(sequentialWithoutPayload), decision => Assert.Equal("dispatch-task", decision.Kind));
        Assert.Equal("complete-stage", zeroGroup.Single().Kind);

        var branchTarget = baseStage with { Id = Id.New(), Key = "shipping", Name = "Shipping", Order = 2 };
        var branchStages = new[] { baseStage, branchTarget };
        var branchInstance = CreateInstance();
        branchInstance.Metadata["Branch.NextStageId"] = JsonValue.Create(branchTarget.Id.ToString())!;

        Assert.Null(InvokeDecisionControlPrivateStatic<StageArtifact>("ResolveBranchTargetStage", branchStages, Array.Empty<StageExecution>(), null!));
        Assert.Null(InvokeDecisionControlPrivateStatic<StageArtifact>("ResolveBranchTargetStage", branchStages, Array.Empty<StageExecution>(), CreateInstance()));
        Assert.Same(branchTarget, InvokeDecisionControlPrivateStatic<StageArtifact>("ResolveBranchTargetStage", branchStages, Array.Empty<StageExecution>(), branchInstance));

        branchInstance.Metadata["Branch.NextStageId"] = JsonValue.Create(" ")!;
        Assert.Null(InvokeDecisionControlPrivateStatic<StageArtifact>("ResolveBranchTargetStage", branchStages, Array.Empty<StageExecution>(), branchInstance));

        branchInstance.Metadata["Branch.NextStageId"] = JsonValue.Create(branchTarget.Id.ToString())!;
        branchInstance.Metadata["Branch.NextStageId"] = JsonValue.Create(Id.New().ToString())!;
        Assert.Null(InvokeDecisionControlPrivateStatic<StageArtifact>("ResolveBranchTargetStage", branchStages, Array.Empty<StageExecution>(), branchInstance));

        branchInstance.Metadata["Branch.NextStageId"] = JsonValue.Create(branchTarget.Id.ToString())!;
        Assert.Null(InvokeDecisionControlPrivateStatic<StageArtifact>(
            "ResolveBranchTargetStage",
            branchStages,
            new[] { new StageExecution { StageKey = branchTarget.Key, Status = StageExecutionStatus.Completed } },
            branchInstance));
    }

    [Fact]
    public void PrivateRetryPolicyHelpersCoverTimeoutAndContinueBranches()
    {
        var retryPolicy = new RetryPolicyArtifact(
            1,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            ["Retryable"],
            true);
        var timeoutPolicy = new TimeoutPolicyArtifact(
            Duration.FromSeconds(5),
            TimeoutBehavior.Reconcile,
            new ReconcileTimeoutBehaviorPolicyArtifact(OrchestrationActionOnTimeout.Block, retryPolicy));
        var waitPolicy = new TimeoutPolicyArtifact(
            Duration.FromSeconds(5),
            TimeoutBehavior.Wait,
            new WaitTimeoutBehaviorPolicyArtifact(OrchestrationActionOnTimeout.Continue, Duration.FromSeconds(1)));
        var artifact = CreateArtifact(task => task with { TimeoutPolicy = timeoutPolicy });
        var taskArtifact = artifact.StageDefinitions.Single().TaskDefinitions.Single();
        var timedOutTask = CreateTaskExecution(TaskExecutionStatus.TimedOut);

        Assert.Same(retryPolicy, InvokeDecisionControlPrivateStatic<RetryPolicyArtifact>("ResolveRetryPolicy", timedOutTask, taskArtifact));
        Assert.Same(taskArtifact.RetryPolicy, InvokeDecisionControlPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            CreateTaskExecution(TaskExecutionStatus.Failed),
            taskArtifact));
        Assert.Same(taskArtifact.RetryPolicy, InvokeDecisionControlPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            timedOutTask,
            taskArtifact with { TimeoutPolicy = waitPolicy }));
        Assert.Same(taskArtifact.RetryPolicy, InvokeDecisionControlPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            timedOutTask,
            taskArtifact with { TimeoutPolicy = null }));
        Assert.Null(InvokeDecisionControlPrivateStatic<RetryPolicyArtifact>(
            "ResolveRetryPolicy",
            timedOutTask,
            null!));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "ShouldContinueAfterFailure",
            timedOutTask,
            taskArtifact with { TimeoutPolicy = waitPolicy }));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "ShouldContinueAfterFailure",
            timedOutTask,
            taskArtifact with { TimeoutPolicy = null }));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "ShouldContinueAfterFailure",
            timedOutTask,
            null!));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "ShouldContinueAfterFailure",
            timedOutTask,
            taskArtifact with
            {
                TimeoutPolicy = waitPolicy with
                {
                    TimeoutBehaviorPolicy = new WaitTimeoutBehaviorPolicyArtifact(
                        OrchestrationActionOnTimeout.Block,
                        Duration.FromSeconds(1))
                }
            }));
        Assert.True(InvokeDecisionControlPrivateStatic<bool>(
            "ShouldContinueAfterFailure",
            CreateTaskExecution(TaskExecutionStatus.Failed),
            taskArtifact with { OnErrorPolicy = OnErrorPolicy.Continue }));
        Assert.False(InvokeDecisionControlPrivateStatic<bool>(
            "ShouldContinueAfterFailure",
            CreateTaskExecution(TaskExecutionStatus.Failed),
            taskArtifact));
    }

    [Fact]
    public void PrivateTryParseIdCoversValidInvalidAndBlankBranches()
    {
        var validId = Id.New();
        var valid = InvokeTryParseId(validId.ToString());
        var invalid = InvokeTryParseId("not-an-id");
        var blank = InvokeTryParseId(" ");

        Assert.True(valid.Parsed);
        Assert.Equal(validId, valid.Id);
        Assert.False(invalid.Parsed);
        Assert.Equal(default, invalid.Id);
        Assert.False(blank.Parsed);
        Assert.Equal(default, blank.Id);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        using var provider = CreateRuntimeServices();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var artifactResolver = services.GetRequiredService<IRuntimeArtifactResolver>();
        var instanceRepository = services.GetRequiredService<IOrchestrationInstanceRepository>();
        var stageRepository = services.GetRequiredService<IStageExecutionRepository>();
        var taskRepository = services.GetRequiredService<ITaskExecutionRepository>();
        var attemptRepository = services.GetRequiredService<ITaskExecutionAttemptRepository>();
        var dispatchRepository = services.GetRequiredService<ITaskDispatchRepository>();
        var payloadState = services.GetRequiredService<IOrchestrationPayloadState>();

        Assert.Equal("artifactResolver", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            null!,
            instanceRepository,
            stageRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            payloadState)).ParamName);
        Assert.Equal("instanceRepository", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            artifactResolver,
            null!,
            stageRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            payloadState)).ParamName);
        Assert.Equal("stageRepository", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            artifactResolver,
            instanceRepository,
            null!,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            payloadState)).ParamName);
        Assert.Equal("taskRepository", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            artifactResolver,
            instanceRepository,
            stageRepository,
            null!,
            attemptRepository,
            dispatchRepository,
            payloadState)).ParamName);
        Assert.Equal("attemptRepository", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            artifactResolver,
            instanceRepository,
            stageRepository,
            taskRepository,
            null!,
            dispatchRepository,
            payloadState)).ParamName);
        Assert.Equal("dispatchRepository", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            artifactResolver,
            instanceRepository,
            stageRepository,
            taskRepository,
            attemptRepository,
            null!,
            payloadState)).ParamName);
        Assert.Equal("payloadState", Assert.Throws<ArgumentNullException>(() => new DecisionControl(
            artifactResolver,
            instanceRepository,
            stageRepository,
            taskRepository,
            attemptRepository,
            dispatchRepository,
            null!)).ParamName);
    }

    [Fact]
    public void PrivateBuildCompleteCallbackDecisionCarriesPayloadAndMetadata()
    {
        var instanceId = Id.New();
        var taskExecutionId = Id.New();
        var dispatchId = Id.New();
        var resultMetadata = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = "Succeeded"
        };
        var request = new DecisionRequest
        {
            Payload = JsonNode.Parse("""{"approved":true}"""),
            ExecutionResultMetadata = resultMetadata
        };

        var decision = InvokeDecisionControlPrivateStatic<CompleteCallbackDecision>(
            "BuildCompleteCallbackDecision",
            request,
            instanceId,
            taskExecutionId,
            dispatchId);

        Assert.NotNull(decision);
        Assert.Equal("complete-callback", decision.Kind);
        Assert.Equal(instanceId, decision.InstanceId);
        Assert.Equal(taskExecutionId, decision.TaskExecutionId);
        Assert.Equal(dispatchId, decision.DispatchId);
        Assert.Equal("""{"approved":true}""", decision.Payload);
        Assert.Same(resultMetadata, decision.ExecutionResultMetadata);
    }

    private static async Task<RetryState> CreateRetryStateAsync(Func<TaskArtifact, TaskArtifact>? configureTask = null)
    {
        var provider = CreateRuntimeServices();
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var artifactRepository = scopedServices.GetRequiredService<IRuntimeArtifactRepository>();
        var instanceRepository = scopedServices.GetRequiredService<IOrchestrationInstanceRepository>();
        var stageRepository = scopedServices.GetRequiredService<IStageExecutionRepository>();
        var taskRepository = scopedServices.GetRequiredService<ITaskExecutionRepository>();

        var artifactId = Id.New();
        var instanceId = Id.New();
        var stageExecutionId = Id.New();
        var taskExecutionId = Id.New();
        var artifact = CreateArtifact(configureTask);

        await artifactRepository.Upsert(new RuntimeOrchestrationArtifact
        {
            Id = artifactId,
            OrchestrationDefinitionKey = artifact.Key,
            ArtifactType = "orchestration-version-snapshot",
            SourceOrchestrationVersionId = artifact.OrchestrationVersionId,
            Version = artifact.Version,
            ArtifactChecksum = artifact.Checksum,
            ArtifactPayload = JsonSerializer.SerializeToNode(artifact, SerializerOptions),
            Status = RuntimeOrchestrationArtifactStatus.Ready,
            IngressGeneration = 1,
            IsActive = true,
            DeployedOnUtc = DateTime.UtcNow
        });
        await instanceRepository.Create(new OrchestrationInstance
        {
            Id = instanceId,
            OrchestrationDefinitionKey = artifact.Key,
            RuntimeOrchestrationArtifactId = artifactId,
            CorrelationId = "sale-1",
            SagaId = instanceId.ToString(),
            ExecutionKey = $"{artifact.Key}:{instanceId}",
            Status = OrchestrationInstanceStatus.Running,
            CurrentStageKey = "inventory",
            CurrentTaskKey = "inventories.reserve",
            SnapshotPayload = JsonNode.Parse(
                """
                {
                  "trigger": {
                    "payload": {
                      "saleId": "sale-1"
                    }
                  },
                  "stages": {},
                  "variables": {}
                }
                """)
        });
        await stageRepository.Create(new StageExecution
        {
            Id = stageExecutionId,
            OrchestrationInstanceId = instanceId,
            StageKey = "inventory",
            Order = 1,
            Status = StageExecutionStatus.Running
        });
        await taskRepository.Create(new TaskExecution
        {
            Id = taskExecutionId,
            OrchestrationInstanceId = instanceId,
            StageExecutionId = stageExecutionId,
            TaskKey = "inventories.reserve",
            TaskKind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Status = TaskExecutionStatus.Failed,
            OnErrorPolicy = OnErrorPolicy.Stop,
            AwaitResponse = true,
            LastAttemptNumber = 1,
            CorrelationId = "sale-1"
        });

        return new RetryState(provider, artifactId, instanceId, stageExecutionId, taskExecutionId);
    }

    private static ServiceProvider CreateRuntimeServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKrackendOrchestrationsRuntime();
        return services.BuildServiceProvider();
    }

    private static OrchestrationArtifact CreateArtifact(Func<TaskArtifact, TaskArtifact>? configureTask = null)
    {
        var retryPolicy = new RetryPolicyArtifact(
            2,
            RetryStrategyType.Fixed,
            new FixedRetryStrategyArtifact(Duration.FromSeconds(1)),
            ["TemporaryInventoryFailure"],
            true);
        var task = new TaskArtifact(
            Id.New(),
            "inventories.reserve",
            "Reserve inventory",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null,
            new TransformationArtifact(EngineType.DSL, new DslTransformationConfigurationArtifact()),
            new MessagingTaskConfigurationArtifact("inventories.reserve", new SemanticVersion(1, 0, 0), null),
            retryPolicy,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWait,
            true);
        task = configureTask?.Invoke(task) ?? task;
        var stage = new StageArtifact(
            Id.New(),
            "inventory",
            "Inventory",
            1,
            true,
            null,
            [task],
            [],
            []);

        return new OrchestrationArtifact(
            Id.New(),
            Id.New(),
            "sales.sale.created",
            "Sale Created",
            "sales",
            new SemanticVersion(1, 0, 0),
            new Checksum("decision-control-retry-test"),
            [],
            [],
            [stage]);
    }

    private static DecisionRequest Request(
        RetryState state,
        string? errorCode = null,
        bool? isRetryableCandidate = null)
        => new()
        {
            ArtifactId = state.ArtifactId.ToString(),
            MessageMetadata = new OrchestrationMessageMetadata
            {
                OrchestrationInstanceId = state.InstanceId.ToString()
            },
            ExecutionResultMetadata = new OrchestrationExecutionResultMetadata
            {
                Succeeded = false,
                ErrorCode = errorCode,
                IsRetryableCandidate = isRetryableCandidate
            },
            Payload = JsonNode.Parse("""{"trigger":{"saleId":"sale-1"}}""")
        };

    private static async Task UpdateInstanceAsync(RetryState state, Action<OrchestrationInstance> configure)
    {
        using var scope = state.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = await repository.GetById(state.InstanceId);
        configure(instance);
        await repository.Update(instance);
    }

    private static async Task UpdateTaskAsync(RetryState state, Action<TaskExecution> configure)
    {
        using var scope = state.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITaskExecutionRepository>();
        var task = await repository.GetById(state.TaskExecutionId);
        configure(task);
        await repository.Update(task);
    }

    private static async Task AddAttemptAsync(RetryState state, Action<TaskExecutionAttempt> configure)
    {
        using var scope = state.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITaskExecutionAttemptRepository>();
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = state.TaskExecutionId,
            AttemptNumber = 1,
            Status = TaskExecutionStatus.Failed
        };
        configure(attempt);
        await repository.Create(attempt);
    }

    private static OrchestrationInstance CreateInstance(OrchestrationInstanceStatus status = OrchestrationInstanceStatus.Running)
    {
        var id = Id.New();
        return new OrchestrationInstance
        {
            Id = id,
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "sale-1",
            SagaId = id.ToString(),
            ExecutionKey = $"sales.sale.created:{id}",
            Status = status
        };
    }

    private static TaskExecution CreateTaskExecution(
        TaskExecutionStatus status = TaskExecutionStatus.Pending,
        string taskKey = "inventories.reserve")
        => new()
        {
            TaskKey = taskKey,
            Status = status
        };

    private static T? InvokeDecisionControlPrivateStatic<T>(string methodName, params object?[] args)
    {
        args ??= [null];
        var method = Assert.Single(typeof(DecisionControl)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == args.Length));
        return (T?)method.Invoke(null, args);
    }

    private static (bool Parsed, Id Id) InvokeTryParseId(string value)
    {
        var method = Assert.Single(typeof(DecisionControl)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(candidate => candidate.Name == "TryParseId"));
        object?[] args = [value, default(Id)];
        var parsed = (bool)method.Invoke(null, args)!;
        return (parsed, (Id)args[1]!);
    }

    private sealed record RetryState(
        IServiceProvider Services,
        Id ArtifactId,
        Id InstanceId,
        Id StageExecutionId,
        Id TaskExecutionId);
}
