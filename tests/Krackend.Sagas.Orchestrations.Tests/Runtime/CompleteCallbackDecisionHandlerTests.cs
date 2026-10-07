namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Reflection;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Artifacts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Handlers;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using NSubstitute;

public sealed class CompleteCallbackDecisionHandlerTests
{
    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            null!,
            StageRepository(),
            TaskRepository(),
            AttemptRepository(),
            DispatchRepository(),
            TransitionRepository(),
            PayloadState(),
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            null!,
            TaskRepository(),
            AttemptRepository(),
            DispatchRepository(),
            TransitionRepository(),
            PayloadState(),
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            null!,
            AttemptRepository(),
            DispatchRepository(),
            TransitionRepository(),
            PayloadState(),
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            TaskRepository(),
            null!,
            DispatchRepository(),
            TransitionRepository(),
            PayloadState(),
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            TaskRepository(),
            AttemptRepository(),
            null!,
            TransitionRepository(),
            PayloadState(),
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            TaskRepository(),
            AttemptRepository(),
            DispatchRepository(),
            null!,
            PayloadState(),
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            TaskRepository(),
            AttemptRepository(),
            DispatchRepository(),
            TransitionRepository(),
            null!,
            ArtifactResolver(),
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            TaskRepository(),
            AttemptRepository(),
            DispatchRepository(),
            TransitionRepository(),
            PayloadState(),
            null!,
            ValidationExecutor()));
        Assert.Throws<ArgumentNullException>(() => new CompleteCallbackDecisionHandler(
            InstanceRepository(),
            StageRepository(),
            TaskRepository(),
            AttemptRepository(),
            DispatchRepository(),
            TransitionRepository(),
            PayloadState(),
            ArtifactResolver(),
            null!));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData("true", null)]
    [InlineData(false, "ok")]
    [InlineData("false", "ok")]
    [InlineData(123, "ok")]
    public void ResolveResponsePayloadHonorsPayloadWasNullMetadata(object marker, string? expectedValue)
    {
        var result = new OrchestrationExecutionResultMetadata();
        result.Metadata[OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey] =
            JsonValue.Create(marker)!;

        var payload = InvokePrivate<JsonNode?>(
            "ResolveResponsePayload",
            """{"status":"ok"}""",
            result);

        if (expectedValue is null)
        {
            Assert.Null(payload);
        }
        else
        {
            Assert.Equal(expectedValue, payload!["status"]!.GetValue<string>());
        }
    }

    [Fact]
    public void ResolveResponsePayloadReturnsNullForWhitespacePayloadAndMissingMetadata()
    {
        var payload = InvokePrivate<JsonNode?>(
            "ResolveResponsePayload",
            "   ",
            new OrchestrationExecutionResultMetadata());

        Assert.Null(payload);
    }

    [Fact]
    public void ResolveResponsePayloadIgnoresUnreadablePayloadWasNullMarker()
    {
        var result = new OrchestrationExecutionResultMetadata();
        result.Metadata[OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey] =
            JsonValue.Create(new Uri("https://example.test"))!;

        var payload = InvokePrivate<JsonNode?>(
            "ResolveResponsePayload",
            """{"status":"ok"}""",
            result);

        Assert.Equal("ok", payload!["status"]!.GetValue<string>());
    }

    [Fact]
    public void GetValidationErrorCodePrefersAdapterThenValidationThenFallback()
    {
        var validation = new ValidationArtifact(EngineType.DSL, new DslValidationConfigurationArtifact())
        {
            ErrorCode = "ConfiguredValidationError"
        };

        var adapterCode = InvokePrivate<string>(
            "GetValidationErrorCode",
            "AdapterValidationError",
            validation,
            "FallbackValidationError");
        var configuredCode = InvokePrivate<string>(
            "GetValidationErrorCode",
            string.Empty,
            validation,
            "FallbackValidationError");
        var fallbackCode = InvokePrivate<string>(
            "GetValidationErrorCode",
            string.Empty,
            null!,
            "FallbackValidationError");

        Assert.Equal("AdapterValidationError", adapterCode);
        Assert.Equal("ConfiguredValidationError", configuredCode);
        Assert.Equal("FallbackValidationError", fallbackCode);
    }

    [Fact]
    public void CopyExecutionResultMetadataCopiesOptionalFieldsAndSkipsPayloadNullMarker()
    {
        var started = new DateTime(2026, 10, 3, 1, 2, 3, DateTimeKind.Utc);
        var completed = started.AddSeconds(4);
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = Id.New(),
            AttemptNumber = 2,
            Status = TaskExecutionStatus.WaitingResponse
        };
        var result = new OrchestrationExecutionResultMetadata
        {
            Succeeded = false,
            Status = "Failed",
            ErrorCode = "InventoryRejected",
            ErrorMessage = "Inventory rejected the reservation.",
            ErrorType = "Business",
            IsRetryableCandidate = false,
            StartedOnUtc = started,
            CompletedOnUtc = completed,
            ExecutionTimeMs = 4000,
            RequestType = "ReserveInventory",
            ResponseType = "ReserveInventoryResponse",
            ServiceName = "Inventory",
            OperationName = "Reserve"
        };
        result.Metadata["diagnostic"] = JsonValue.Create("value")!;
        result.Metadata[OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey] =
            JsonValue.Create(true)!;

        InvokePrivate<object?>("CopyExecutionResultMetadata", attempt, result);

        Assert.False(attempt.Metadata["ExecutionSucceeded"]!.GetValue<bool>());
        Assert.Equal("Failed", attempt.Metadata["ExecutionStatus"]!.GetValue<string>());
        Assert.Equal("InventoryRejected", attempt.Metadata["ExecutionErrorCode"]!.GetValue<string>());
        Assert.Equal("Inventory rejected the reservation.", attempt.Metadata["ExecutionErrorMessage"]!.GetValue<string>());
        Assert.Equal("Business", attempt.Metadata["ExecutionErrorType"]!.GetValue<string>());
        Assert.False(attempt.Metadata["ExecutionIsRetryableCandidate"]!.GetValue<bool>());
        Assert.Equal(started, attempt.Metadata["ExecutionStartedOnUtc"]!.GetValue<DateTime>());
        Assert.Equal(completed, attempt.Metadata["ExecutionCompletedOnUtc"]!.GetValue<DateTime>());
        Assert.Equal(4000, attempt.Metadata["ExecutionTimeMs"]!.GetValue<long>());
        Assert.Equal("ReserveInventory", attempt.Metadata["ExecutionRequestType"]!.GetValue<string>());
        Assert.Equal("ReserveInventoryResponse", attempt.Metadata["ExecutionResponseType"]!.GetValue<string>());
        Assert.Equal("Inventory", attempt.Metadata["ExecutionServiceName"]!.GetValue<string>());
        Assert.Equal("Reserve", attempt.Metadata["ExecutionOperationName"]!.GetValue<string>());
        Assert.Equal("value", attempt.Metadata["Execution.diagnostic"]!.GetValue<string>());
        Assert.False(attempt.Metadata.ContainsKey(
            $"Execution.{OrchestrationMetadataConstants.OrchestrationPayloadWasNullMetadataKey}"));
    }

    [Fact]
    public void CopyExecutionResultMetadataHandlesNullStatusAndNullCustomMetadataValues()
    {
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = Id.New(),
            AttemptNumber = 1,
            Status = TaskExecutionStatus.WaitingResponse
        };
        var result = new OrchestrationExecutionResultMetadata
        {
            Succeeded = true,
            Status = null
        };
        result.Metadata["nullable"] = null!;

        InvokePrivate<object?>("CopyExecutionResultMetadata", attempt, result);

        Assert.Equal(string.Empty, attempt.Metadata["ExecutionStatus"]!.GetValue<string>());
        Assert.True(attempt.Metadata.ContainsKey("Execution.nullable"));
        Assert.Null(attempt.Metadata["Execution.nullable"]);
    }

    [Fact]
    public void CanApplyCallbackCoversReadyLateAndRejectedStateBranches()
    {
        var ready = CallbackState();
        Assert.True(InvokePrivate<bool>(
            "CanApplyCallback",
            ready.Instance,
            ready.Task,
            ready.Attempt,
            ready.Dispatch,
            true));

        var hardTerminal = CallbackState(instanceStatus: OrchestrationInstanceStatus.Completed);
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            hardTerminal.Instance,
            hardTerminal.Task,
            hardTerminal.Attempt,
            hardTerminal.Dispatch,
            true));

        var mismatchedTask = CallbackState();
        mismatchedTask.Task.OrchestrationInstanceId = Id.New();
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            mismatchedTask.Instance,
            mismatchedTask.Task,
            mismatchedTask.Attempt,
            mismatchedTask.Dispatch,
            true));

        var mismatchedAttempt = CallbackState();
        mismatchedAttempt.Attempt.TaskExecutionId = Id.New();
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            mismatchedAttempt.Instance,
            mismatchedAttempt.Task,
            mismatchedAttempt.Attempt,
            mismatchedAttempt.Dispatch,
            true));

        var mismatchedDispatch = CallbackState();
        mismatchedDispatch.Dispatch.TaskExecutionAttemptId = Id.New();
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            mismatchedDispatch.Instance,
            mismatchedDispatch.Task,
            mismatchedDispatch.Attempt,
            mismatchedDispatch.Dispatch,
            true));

        var lateTimedOut = CallbackState(
            instanceStatus: OrchestrationInstanceStatus.Failed,
            taskStatus: TaskExecutionStatus.Running,
            attemptStatus: TaskExecutionStatus.TimedOut,
            dispatchStatus: "RetryPending");
        Assert.True(InvokePrivate<bool>(
            "CanApplyCallback",
            lateTimedOut.Instance,
            lateTimedOut.Task,
            lateTimedOut.Attempt,
            lateTimedOut.Dispatch,
            true));

        var lateFinishedDispatch = CallbackState(
            instanceStatus: OrchestrationInstanceStatus.DeadLettered,
            taskStatus: TaskExecutionStatus.Running,
            attemptStatus: TaskExecutionStatus.Running,
            dispatchStatus: "Acknowledged");
        Assert.True(InvokePrivate<bool>(
            "CanApplyCallback",
            lateFinishedDispatch.Instance,
            lateFinishedDispatch.Task,
            lateFinishedDispatch.Attempt,
            lateFinishedDispatch.Dispatch,
            true));

        var repeatedCompleted = CallbackState(
            taskStatus: TaskExecutionStatus.Completed,
            attemptStatus: TaskExecutionStatus.TimedOut,
            dispatchStatus: "Acknowledged");
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            repeatedCompleted.Instance,
            repeatedCompleted.Task,
            repeatedCompleted.Attempt,
            repeatedCompleted.Dispatch,
            true));

        var staleAttempt = CallbackState(
            taskStatus: TaskExecutionStatus.Running,
            attemptStatus: TaskExecutionStatus.TimedOut,
            dispatchStatus: "Acknowledged");
        staleAttempt.Task.LastAttemptNumber = 2;
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            staleAttempt.Instance,
            staleAttempt.Task,
            staleAttempt.Attempt,
            staleAttempt.Dispatch,
            true));

        var nonTerminalInstance = CallbackState(
            instanceStatus: OrchestrationInstanceStatus.Created,
            taskStatus: TaskExecutionStatus.Running,
            attemptStatus: TaskExecutionStatus.TimedOut,
            dispatchStatus: "Acknowledged");
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            nonTerminalInstance.Instance,
            nonTerminalInstance.Task,
            nonTerminalInstance.Attempt,
            nonTerminalInstance.Dispatch,
            true));

        var unfinishedDispatch = CallbackState(
            taskStatus: TaskExecutionStatus.Running,
            attemptStatus: TaskExecutionStatus.Running,
            dispatchStatus: "Scheduled");
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            unfinishedDispatch.Instance,
            unfinishedDispatch.Task,
            unfinishedDispatch.Attempt,
            unfinishedDispatch.Dispatch,
            true));

        var failedCallback = CallbackState(
            taskStatus: TaskExecutionStatus.Running,
            attemptStatus: TaskExecutionStatus.TimedOut,
            dispatchStatus: "Acknowledged");
        Assert.False(InvokePrivate<bool>(
            "CanApplyCallback",
            failedCallback.Instance,
            failedCallback.Task,
            failedCallback.Attempt,
            failedCallback.Dispatch,
            false));
    }

    private static T InvokePrivate<T>(string methodName, params object?[] arguments)
        => (T)typeof(CompleteCallbackDecisionHandler)
            .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments)!;

    private static CallbackRuntimeState CallbackState(
        OrchestrationInstanceStatus instanceStatus = OrchestrationInstanceStatus.Waiting,
        TaskExecutionStatus taskStatus = TaskExecutionStatus.WaitingResponse,
        TaskExecutionStatus attemptStatus = TaskExecutionStatus.WaitingResponse,
        string dispatchStatus = "WaitingResponse")
    {
        var instance = new OrchestrationInstance
        {
            Id = Id.New(),
            RuntimeOrchestrationArtifactId = Id.New(),
            TriggerIntakeId = Id.New(),
            Status = instanceStatus,
            OrchestrationDefinitionKey = "sales.sale.created",
            CorrelationId = "corr-callback",
            SagaId = "saga-callback",
            ExecutionKey = "sales.sale.created:1.0.0",
            StartedOnUtc = DateTime.UtcNow,
            LastUpdatedOnUtc = DateTime.UtcNow
        };
        var task = new TaskExecution
        {
            Id = Id.New(),
            OrchestrationInstanceId = instance.Id,
            StageExecutionId = Id.New(),
            TaskKey = "inventories.reserve",
            LastAttemptNumber = 1,
            Status = taskStatus
        };
        var attempt = new TaskExecutionAttempt
        {
            Id = Id.New(),
            TaskExecutionId = task.Id,
            AttemptNumber = 1,
            Status = attemptStatus
        };
        var dispatch = new TaskDispatch
        {
            Id = Id.New(),
            TaskExecutionAttemptId = attempt.Id,
            DispatchStatus = dispatchStatus,
            Destination = "commands.inventory.reserve",
            DispatchType = "Messaging",
            ScheduledOnUtc = DateTime.UtcNow
        };

        return new CallbackRuntimeState(instance, task, attempt, dispatch);
    }

    private static IOrchestrationInstanceRepository InstanceRepository()
        => Substitute.For<IOrchestrationInstanceRepository>();

    private static IStageExecutionRepository StageRepository()
        => Substitute.For<IStageExecutionRepository>();

    private static ITaskExecutionRepository TaskRepository()
        => Substitute.For<ITaskExecutionRepository>();

    private static ITaskExecutionAttemptRepository AttemptRepository()
        => Substitute.For<ITaskExecutionAttemptRepository>();

    private static ITaskDispatchRepository DispatchRepository()
        => Substitute.For<ITaskDispatchRepository>();

    private static IExecutionTransitionRepository TransitionRepository()
        => Substitute.For<IExecutionTransitionRepository>();

    private static IOrchestrationPayloadState PayloadState()
        => Substitute.For<IOrchestrationPayloadState>();

    private static IRuntimeArtifactResolver ArtifactResolver()
        => Substitute.For<IRuntimeArtifactResolver>();

    private static IOrchestrationValidationExecutor ValidationExecutor()
        => Substitute.For<IOrchestrationValidationExecutor>();

    private sealed record CallbackRuntimeState(
        OrchestrationInstance Instance,
        TaskExecution Task,
        TaskExecutionAttempt Attempt,
        TaskDispatch Dispatch);
}
