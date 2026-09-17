namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Client.Errors;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Client.Publishing;
using Krackend.Sagas.Orchestrations.Runtime.Engine;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.SchemaRegistry;
using Krackend.Sagas.Orchestrations.Tests.Client.Support;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Fakes;
using Krackend.Sagas.Orchestrations.Tests.Runtime.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
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
    public async Task EnginePromotesWhenTriggerValidationSucceedsAndPassesValidationContext()
    {
        OrchestrationValidationRequest? capturedRequest = null;
        var validationExecutor = Substitute.For<IOrchestrationValidationExecutor>();
        validationExecutor
            .ValidateAsync(
                Arg.Do<OrchestrationValidationRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(OrchestrationValidationResult.Success());
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(
                [EventTrigger(validationEnabled: true)],
                Stage("stage-one", 1, MessagingTask("task.after.trigger.validation", 1))),
            services => services.Replace(ServiceDescriptor.Scoped(_ => validationExecutor)));

        await harness.StartAsync(BusinessPayload("validated-trigger"), "correlation-trigger-validation");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Trigger", capturedRequest.Phase);
        Assert.NotNull(capturedRequest.Trigger);
        Assert.Equal("trigger payload validation", capturedRequest.ValidationDsl);
        Assert.Equal("validated-trigger", capturedRequest.Payload!["value"]!.GetValue<string>());
        Assert.Single(harness.Dispatcher.Commands);
    }

    [Fact]
    public async Task EngineRejectsPromotionWhenTriggerValidationFails()
    {
        var validationExecutor = Substitute.For<IOrchestrationValidationExecutor>();
        validationExecutor
            .ValidateAsync(Arg.Any<OrchestrationValidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationValidationResult.Failure(
                string.Empty,
                string.Empty,
                new Dictionary<string, JsonNode> { ["field"] = JsonValue.Create("missing")! }));
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(
                [EventTrigger(validationEnabled: true)],
                Stage("stage-one", 1, MessagingTask("task.not.dispatched", 1))),
            services => services.Replace(ServiceDescriptor.Scoped(_ => validationExecutor)));

        await harness.StartAsync(BusinessPayload("invalid-trigger"), "correlation-trigger-invalid");

        var instances = await harness.GetRequiredService<IOrchestrationInstanceRepository>().GetRecent();
        Assert.Empty(instances);
        Assert.Empty(harness.Dispatcher.Commands);
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
    public async Task EngineSchedulesRetryUsingFixedRetryDelay()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.delayed.retry",
                1,
                retryPolicy: RetryPolicy(1, "TransientFailure")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-delayed-retry");

        var failedCommand = harness.Dispatcher.Commands.Single();
        var failedOnUtc = DateTimeOffset.UtcNow;
        await harness.ForwardAsync(failedCommand, null, Failure("TransientFailure", "temporary outage"));

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var retryCommand = harness.Dispatcher.Commands[1];

        Assert.Equal("task.delayed.retry", retryCommand.TaskKey);
        Assert.NotNull(retryCommand.ScheduledOnUtc);
        Assert.True(retryCommand.ScheduledOnUtc.Value >= failedOnUtc.AddMilliseconds(500));
        Assert.Equal(2, retryCommand.MessageMetadata.Attempt);
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
    public async Task EngineIgnoresCallbackWhenDispatchIdDoesNotMatchRuntimeState()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.dispatch.guard", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-invalid-dispatch");

        var command = harness.Dispatcher.Commands.Single();
        command.MessageMetadata.DispatchId = Id.New().ToString();
        await harness.ForwardAsync(command, BusinessPayload("invalid-dispatch-callback"), Success());

        var task = await harness.GetTaskAsync(command);
        var attempt = await harness.GetAttemptAsync(command);
        var transitions = await harness.GetTransitionsAsync(command);

        Assert.Equal(TaskExecutionStatus.WaitingResponse, task.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, attempt.Status);
        Assert.DoesNotContain(transitions, transition => transition.TransitionType == "TaskCallbackCompleted");
    }

    [Fact]
    public async Task EngineIgnoresCallbackWhenAttemptNumberDoesNotMatchRuntimeState()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask("task.attempt.guard", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-invalid-attempt");

        var command = harness.Dispatcher.Commands.Single();
        command.MessageMetadata.Attempt = 2;
        await harness.ForwardAsync(command, BusinessPayload("invalid-attempt-callback"), Success());

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
    public async Task EngineMarksRetryConfigurationFailedWhenRetryingUnsupportedTaskKind()
    {
        var task = HttpTask(
            "task.http.retry.invalid",
            1,
            retryPolicy: RetryPolicy(2, "HttpTransient"));
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, task)));
        var seeded = await harness.SeedFailedTaskAsync("stage-one", task, "HttpTransient");

        await harness.Engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = harness.ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = seeded.Instance.SagaId,
                OrchestrationInstanceId = seeded.Instance.Id.ToString(),
                CorrelationId = seeded.Instance.CorrelationId
            },
            Payload = BusinessPayload("retry-signal")
        });

        var instance = await harness.GetRequiredService<IOrchestrationInstanceRepository>().GetById(seeded.Instance.Id);
        var persistedTask = await harness.GetRequiredService<ITaskExecutionRepository>().GetById(seeded.Task.Id);
        var attempts = await harness.GetRequiredService<ITaskExecutionAttemptRepository>().GetByTaskExecutionId(seeded.Task.Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(seeded.Instance.Id);

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, persistedTask.Status);
        Assert.True(persistedTask.Metadata["RetrySuppressed"]!.GetValue<bool>());
        Assert.Contains("Task kind 'Http'", persistedTask.Metadata["RetryConfigurationError"]!.GetValue<string>());
        Assert.Single(attempts);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskRetryConfigurationFailed");
    }

    [Fact]
    public async Task EngineMarksRetryConfigurationFailedWhenMessagingRetryHasNoConfiguration()
    {
        var task = MisconfiguredMessagingTask(
            "task.messaging.retry.invalid",
            1,
            RetryPolicy(2, "MessagingTransient"));
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, task)));
        var seeded = await harness.SeedFailedTaskAsync("stage-one", task, "MessagingTransient");

        await harness.Engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = harness.ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = seeded.Instance.SagaId,
                OrchestrationInstanceId = seeded.Instance.Id.ToString(),
                CorrelationId = seeded.Instance.CorrelationId
            },
            Payload = BusinessPayload("retry-signal")
        });

        var instance = await harness.GetRequiredService<IOrchestrationInstanceRepository>().GetById(seeded.Instance.Id);
        var persistedTask = await harness.GetRequiredService<ITaskExecutionRepository>().GetById(seeded.Task.Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(seeded.Instance.Id);

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.True(persistedTask.Metadata["RetrySuppressed"]!.GetValue<bool>());
        Assert.Contains("does not contain a messaging configuration", persistedTask.Metadata["RetryConfigurationError"]!.GetValue<string>());
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskRetryConfigurationFailed");
    }

    [Fact]
    public async Task EngineMarksRetryPreparationFailedWhenRetryPayloadPreparationFails()
    {
        var task = MessagingTask(
            "task.retry.prepare.fail",
            1,
            retryPolicy: RetryPolicy(2, "PreparationTransient"));
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(Stage("stage-one", 1, task)),
            services =>
            {
                services.AddScoped<SequencedTaskDispatchRequestPayloadPreparer>();
                services.Replace(ServiceDescriptor.Scoped<ITaskDispatchRequestPayloadPreparer>(provider =>
                    provider.GetRequiredService<SequencedTaskDispatchRequestPayloadPreparer>()));
            });
        var preparer = harness.GetRequiredService<SequencedTaskDispatchRequestPayloadPreparer>();
        preparer.FailNext("RetryPreparationFailed", "retry payload cannot be prepared");
        var seeded = await harness.SeedFailedTaskAsync("stage-one", task, "PreparationTransient");

        await harness.Engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = harness.ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = seeded.Instance.SagaId,
                OrchestrationInstanceId = seeded.Instance.Id.ToString(),
                CorrelationId = seeded.Instance.CorrelationId
            },
            Payload = BusinessPayload("retry-signal")
        });

        var instance = await harness.GetRequiredService<IOrchestrationInstanceRepository>().GetById(seeded.Instance.Id);
        var attempts = (await harness.GetRequiredService<ITaskExecutionAttemptRepository>()
            .GetByTaskExecutionId(seeded.Task.Id)).OrderBy(x => x.AttemptNumber).ToArray();
        var dispatch = await harness.GetRequiredService<ITaskDispatchRepository>().GetByAttemptId(attempts[1].Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(seeded.Instance.Id);

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(2, attempts.Length);
        Assert.Equal("PreparationTransient", attempts[0].ErrorCode);
        Assert.Equal("RetryPreparationFailed", attempts[1].ErrorCode);
        Assert.Equal("RetryPreparationFailed", attempts[1].Metadata["PreparationErrorCode"]!.GetValue<string>());
        Assert.Equal("Failed", dispatch.DispatchStatus);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskRetryPreparationFailed");
    }

    [Fact]
    public async Task EngineMarksRetryQueueFailedWhenCallbackRetryHasNoBackchannel()
    {
        var task = MessagingTask(
            "task.retry.no.backchannel",
            1,
            retryPolicy: RetryPolicy(1, "TransientFailure"));
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(Stage("stage-one", 1, task)),
            services => services.Replace(ServiceDescriptor.Scoped<IGetIngressConfigurationByArtifactAccessor, EmptyIngressConfigurationAccessor>()));
        var seeded = await harness.SeedFailedTaskAsync("stage-one", task, "TransientFailure");

        await harness.Engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = harness.ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = seeded.Instance.SagaId,
                OrchestrationInstanceId = seeded.Instance.Id.ToString(),
                CorrelationId = seeded.Instance.CorrelationId
            },
            Payload = BusinessPayload("retry-signal")
        });

        var instance = await harness.GetRequiredService<IOrchestrationInstanceRepository>().GetById(seeded.Instance.Id);
        var attempts = (await harness.GetRequiredService<ITaskExecutionAttemptRepository>()
            .GetByTaskExecutionId(seeded.Task.Id)).OrderBy(x => x.AttemptNumber).ToArray();
        var dispatch = await harness.GetRequiredService<ITaskDispatchRepository>().GetByAttemptId(attempts[1].Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(seeded.Instance.Id);

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal("CommandDispatchFailed", attempts[1].ErrorCode);
        Assert.Equal("Failed", dispatch.DispatchStatus);
        Assert.Contains("requires a backchannel reply address", dispatch.FailureReason, StringComparison.Ordinal);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskRetryQueueFailed");
    }

    [Fact]
    public async Task EngineAllowsFireAndForgetRetryWithoutBackchannel()
    {
        var task = MessagingTask(
            "task.retry.fire.and.forget",
            1,
            retryPolicy: RetryPolicy(1, "TransientFailure"),
            dispatchType: TaskDispatchType.FireAndForget);
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(Stage("stage-one", 1, task)),
            services => services.Replace(ServiceDescriptor.Scoped<IGetIngressConfigurationByArtifactAccessor, EmptyIngressConfigurationAccessor>()));
        var seeded = await harness.SeedFailedTaskAsync("stage-one", task, "TransientFailure");

        await harness.Engine.OrchestrateAsync(new ForwardIntent
        {
            ArtifactId = harness.ArtifactId.ToString(),
            IngressTransport = IngressTransport.Messaging,
            MessageMetadata = new OrchestrationMessageMetadata
            {
                SagaId = seeded.Instance.SagaId,
                OrchestrationInstanceId = seeded.Instance.Id.ToString(),
                CorrelationId = seeded.Instance.CorrelationId
            },
            Payload = BusinessPayload("retry-signal")
        });

        var command = Assert.Single(harness.Dispatcher.Commands);
        var attempts = (await harness.GetRequiredService<ITaskExecutionAttemptRepository>()
            .GetByTaskExecutionId(seeded.Task.Id)).OrderBy(x => x.AttemptNumber).ToArray();
        var dispatch = await harness.GetRequiredService<ITaskDispatchRepository>().GetByAttemptId(attempts[1].Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(seeded.Instance.Id);

        Assert.False(command.AwaitResponse);
        Assert.Null(command.MessageMetadata.ReplyAddress);
        Assert.Equal(2, command.MessageMetadata.Attempt);
        Assert.NotNull(command.ScheduledOnUtc);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
        Assert.Equal("Completed", dispatch.DispatchStatus);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskRetryScheduled");
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
    public async Task EngineFailsCallbackWhenConfiguredResponseValidationFails()
    {
        OrchestrationValidationRequest? capturedRequest = null;
        var validationExecutor = Substitute.For<IOrchestrationValidationExecutor>();
        validationExecutor
            .ValidateAsync(
                Arg.Do<OrchestrationValidationRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(OrchestrationValidationResult.Failure(
                string.Empty,
                "response contract was not satisfied",
                new Dictionary<string, JsonNode> { ["field"] = JsonValue.Create("reserved")! }));
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(Stage("stage-one", 1, MessagingTaskWithResponseValidation("task.response.validation", 1))),
            services => services.Replace(ServiceDescriptor.Scoped(_ => validationExecutor)));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-response-validation");

        var command = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(command, BusinessPayload("invalid-response"), Success("inventory", "reserve"));

        var instance = await harness.GetInstanceAsync(command);
        var task = await harness.GetTaskAsync(command);
        var attempt = await harness.GetAttemptAsync(command);
        var transitions = await harness.GetTransitionsAsync(command);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Response", capturedRequest.Phase);
        Assert.Equal("response validation dsl", capturedRequest.ValidationDsl);
        Assert.Equal("invalid-response", capturedRequest.Payload!["value"]!.GetValue<string>());
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal("ResponseShapeInvalid", attempt.ErrorCode);
        Assert.Equal("Validation", attempt.Metadata["ExecutionErrorType"]!.GetValue<string>());
        Assert.Equal("reserved", attempt.Metadata["Execution.Validation.field"]!.GetValue<string>());
        Assert.Equal("invalid-response", attempt.ResponsePayload!["value"]!.GetValue<string>());
        Assert.False(attempt.ResponsePayload.AsObject().ContainsKey(nameof(OrchestrationExecutionResultMetadata.ErrorCode)));
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskCallbackFailed");
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

    [Fact]
    public async Task TimeoutProcessorCompletesWhenReconcileRetryEventuallySucceeds()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.timeout.reconcile.success",
                1,
                timeoutPolicy: ReconcileTimeoutPolicy(
                    Duration.FromSeconds(1),
                    RetryPolicy(1, "TaskTimedOut"))))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-reconcile-success");

        var firstCommand = harness.Dispatcher.Commands.Single();
        var firstTask = await harness.GetTaskAsync(firstCommand);
        var processor = harness.GetRequiredService<IOrchestrationTimeoutProcessor>();
        await processor.ProcessDueTimeoutsAsync(firstTask.WaitingSinceUtc!.Value.AddSeconds(2));

        var retryCommand = harness.Dispatcher.Commands[1];
        await harness.ForwardAsync(retryCommand, BusinessPayload("reconcile-response"), Success());

        var instance = await harness.GetInstanceAsync(firstCommand);
        var attempts = (await harness.GetAttemptsAsync(firstCommand)).OrderBy(x => x.AttemptNumber).ToArray();

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(TaskExecutionStatus.TimedOut, attempts[0].Status);
        Assert.Equal(TaskExecutionStatus.Completed, attempts[1].Status);
    }

    [Fact]
    public async Task EngineSkipsStageWhenExecutionConditionIsFalseAndContinues()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-skipped", 1, EnabledCondition("false"), MessagingTask("task.not.sent", 1)),
            Stage("stage-next", 2, MessagingTask("task.after.stage.skip", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-stage-condition");

        var command = harness.Dispatcher.Commands.Single();
        Assert.Equal("task.after.stage.skip", command.TaskKey);

        await harness.ForwardAsync(command, BusinessPayload("stage-condition-response"), Success());

        var instance = await harness.GetInstanceAsync(command);
        var stages = (await harness.GetStagesAsync(command)).OrderBy(x => x.Order).ToArray();
        var transitions = await harness.GetTransitionsAsync(command);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Equal(StageExecutionStatus.Skipped, stages[0].Status);
        Assert.Equal(StageExecutionStatus.Completed, stages[1].Status);
        Assert.Contains(transitions, transition => transition.TransitionType == "StageSkipped");
    }

    [Fact]
    public async Task EngineFailsStageWhenEnabledConditionCannotBeEvaluated()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-expression", 1, EnabledCondition("$trigger.value == 'allowed'"), MessagingTask("task.not.sent", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-stage-condition-failure");

        var instanceRepository = harness.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = (await instanceRepository.GetRecent()).Single();
        var stages = await harness.GetRequiredService<IStageExecutionRepository>().GetByInstanceId(instance.Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(instance.Id);

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(StageExecutionStatus.Failed, stages.Single().Status);
        Assert.Contains(transitions, transition => transition.TransitionType == "StageConditionFailed");
    }

    [Fact]
    public async Task EngineSkipsTaskWhenExecutionConditionIsFalseAndContinues()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage(
                "stage-one",
                1,
                MessagingTask("task.skipped", 1, executionCondition: EnabledCondition("false")),
                MessagingTask("task.after.task.skip", 2))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-task-condition");

        var command = harness.Dispatcher.Commands.Single();
        Assert.Equal("task.after.task.skip", command.TaskKey);

        await harness.ForwardAsync(command, BusinessPayload("task-condition-response"), Success());

        var instance = await harness.GetInstanceAsync(command);
        var tasks = (await harness.GetTasksAsync(command)).OrderBy(x => x.TaskKey).ToArray();
        var transitions = await harness.GetTransitionsAsync(command);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Contains(tasks, task => task.TaskKey == "task.skipped" && task.Status == TaskExecutionStatus.Skipped);
        Assert.Contains(tasks, task => task.TaskKey == "task.after.task.skip" && task.Status == TaskExecutionStatus.Completed);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskSkipped");
    }

    [Fact]
    public async Task EngineFailsTaskWhenExecutionConditionCannotBeEvaluated()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage(
                "stage-one",
                1,
                MessagingTask("task.condition.failure", 1, executionCondition: EnabledCondition("$trigger.value == 'allowed'")))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-task-condition-failure");

        var instanceRepository = harness.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = (await instanceRepository.GetRecent()).Single();
        var tasks = await harness.GetRequiredService<ITaskExecutionRepository>().GetByInstanceId(instance.Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(instance.Id);
        var task = tasks.Single();

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal("task.condition.failure", task.TaskKey);
        Assert.True(task.Metadata.ContainsKey("ConditionErrorCode"));
        Assert.True(task.Metadata["RetrySuppressed"]!.GetValue<bool>());
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskConditionFailed");
    }

    [Fact]
    public async Task EngineFailsUnsupportedTaskKindWithoutDispatchingCommand()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage(
                "stage-one",
                1,
                HttpTask("task.http.unsupported", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-unsupported-task");

        var instanceRepository = harness.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = (await instanceRepository.GetRecent()).Single();
        var tasks = await harness.GetRequiredService<ITaskExecutionRepository>().GetByInstanceId(instance.Id);
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(instance.Id);
        var task = tasks.Single();

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal(TaskKind.Http, task.TaskKind);
        Assert.Equal("UnsupportedTaskKind", task.Metadata["ExecutionErrorCode"]!.GetValue<string>());
        Assert.True(task.Metadata["RetrySuppressed"]!.GetValue<bool>());
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskDispatchUnsupported");
    }

    [Fact]
    public async Task EngineFailsCallbackTaskWhenBackchannelIsMissing()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(
            CreateArtifact(
                Stage(
                    "stage-one",
                    1,
                    MessagingTask("task.no.backchannel", 1))),
            services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IGetIngressConfigurationByArtifactAccessor>(_ =>
                    new StaticIngressConfigurationByArtifactAccessor([])));
            });

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-missing-backchannel");

        var instanceRepository = harness.GetRequiredService<IOrchestrationInstanceRepository>();
        var instance = (await instanceRepository.GetRecent()).Single();
        var tasks = await harness.GetRequiredService<ITaskExecutionRepository>().GetByInstanceId(instance.Id);
        var dispatches = await harness.GetRequiredService<ITaskDispatchRepository>().GetScheduledOlderThan(DateTime.UtcNow.AddMinutes(1));
        var transitions = await harness.GetRequiredService<IExecutionTransitionRepository>().GetByInstanceId(instance.Id);
        var task = tasks.Single();
        var dispatch = dispatches.Single();

        Assert.Empty(harness.Dispatcher.Commands);
        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.Failed, task.Status);
        Assert.Equal("Failed", dispatch.DispatchStatus);
        Assert.Contains("requires a backchannel reply address", dispatch.FailureReason, StringComparison.Ordinal);
        Assert.Contains(transitions, transition => transition.TransitionType == "TaskDispatchQueueFailed");
    }

    [Fact]
    public async Task EngineNavigatesForwardBranchAndSkipsIntermediateStages()
    {
        var sourceStageId = Id.New();
        var targetStageId = Id.New();
        var branch = new BranchRuleArtifact(
            Id.New(),
            ElementType.Stage,
            sourceStageId,
            EnabledCondition("true"),
            ElementType.Stage,
            targetStageId);

        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            StageWithGraph(sourceStageId, "stage-source", 1, [], [branch], MessagingTask("task.source", 1)),
            Stage("stage-middle", 2, MessagingTask("task.middle", 1)),
            StageWithGraph(targetStageId, "stage-target", 3, [], [], MessagingTask("task.target", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-branch");

        var sourceCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(sourceCommand, BusinessPayload("source-response"), Success());

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var targetCommand = harness.Dispatcher.Commands[1];
        Assert.Equal("task.target", targetCommand.TaskKey);

        await harness.ForwardAsync(targetCommand, BusinessPayload("target-response"), Success());

        var instance = await harness.GetInstanceAsync(sourceCommand);
        var stages = (await harness.GetStagesAsync(sourceCommand)).OrderBy(x => x.Order).ToArray();
        var transitions = await harness.GetTransitionsAsync(sourceCommand);

        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
        Assert.Contains(stages, stage => stage.StageKey == "stage-middle" && stage.Status == StageExecutionStatus.Skipped);
        Assert.DoesNotContain(harness.Dispatcher.Commands, command => command.TaskKey == "task.middle");
        Assert.Contains(transitions, transition => transition.TransitionType == "BranchTaken");
    }

    [Fact]
    public async Task EngineFailsStageWhenBranchConditionCannotBeEvaluated()
    {
        var sourceStageId = Id.New();
        var targetStageId = Id.New();
        var branch = new BranchRuleArtifact(
            Id.New(),
            ElementType.Stage,
            sourceStageId,
            EnabledCondition("$trigger.value == 'allowed'"),
            ElementType.Stage,
            targetStageId);

        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            StageWithGraph(sourceStageId, "stage-source", 1, [], [branch], MessagingTask("task.source", 1)),
            StageWithGraph(targetStageId, "stage-target", 2, [], [], MessagingTask("task.target", 1))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-branch-failure");

        var sourceCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(sourceCommand, BusinessPayload("source-response"), Success());

        var instance = await harness.GetInstanceAsync(sourceCommand);
        var stages = await harness.GetStagesAsync(sourceCommand);
        var sourceStage = stages.Single(stage => stage.StageKey == "stage-source");
        var transitions = await harness.GetTransitionsAsync(sourceCommand);

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(StageExecutionStatus.Failed, sourceStage.Status);
        Assert.Equal("ConditionAdapterNotConfigured", sourceStage.Metadata["BranchErrorCode"]!.GetValue<string>());
        Assert.Contains("requires a DSL condition adapter", sourceStage.Metadata["BranchErrorMessage"]!.GetValue<string>());
        Assert.DoesNotContain(harness.Dispatcher.Commands, command => command.TaskKey == "task.target");
        Assert.Contains(transitions, transition => transition.TransitionType == "BranchEvaluationFailed");
    }

    [Fact]
    public async Task EngineDispatchesParallelTasksAndWaitsForAllBeforeContinuing()
    {
        var groupId = Id.New();

        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            StageWithGraph(
                Id.New(),
                "stage-parallel",
                1,
                [new ParallelGroupArtifact(groupId, ParallelJoinPolicy.WaitAll, null)],
                [],
                MessagingTask("task.parallel.a", 1, executionMode: TaskExecutionMode.Parallel, parallelGroupId: groupId),
                MessagingTask("task.parallel.b", 2, executionMode: TaskExecutionMode.Parallel, parallelGroupId: groupId),
                MessagingTask("task.after.parallel", 3))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-parallel");

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var firstParallel = harness.Dispatcher.Commands.Single(command => command.TaskKey == "task.parallel.a");
        var secondParallel = harness.Dispatcher.Commands.Single(command => command.TaskKey == "task.parallel.b");

        await harness.ForwardAsync(firstParallel, BusinessPayload("parallel-a"), Success());
        Assert.Equal(2, harness.Dispatcher.Commands.Count);

        await harness.ForwardAsync(secondParallel, BusinessPayload("parallel-b"), Success());

        Assert.Equal(3, harness.Dispatcher.Commands.Count);
        var afterParallel = harness.Dispatcher.Commands[2];
        Assert.Equal("task.after.parallel", afterParallel.TaskKey);

        await harness.ForwardAsync(afterParallel, BusinessPayload("after-parallel"), Success());

        var instance = await harness.GetInstanceAsync(firstParallel);
        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task EngineHonorsConfiguredParallelAgentLimit()
    {
        var groupId = Id.New();

        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            StageWithGraph(
                Id.New(),
                "stage-throttled-parallel",
                1,
                [new ParallelGroupArtifact(groupId, ParallelJoinPolicy.WaitAll, 1)],
                [],
                MessagingTask("task.parallel.one", 1, executionMode: TaskExecutionMode.Parallel, parallelGroupId: groupId),
                MessagingTask("task.parallel.two", 2, executionMode: TaskExecutionMode.Parallel, parallelGroupId: groupId),
                MessagingTask("task.after.parallel.limit", 3))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-parallel-limit");

        Assert.Single(harness.Dispatcher.Commands);
        var firstParallel = harness.Dispatcher.Commands[0];
        Assert.Equal("task.parallel.one", firstParallel.TaskKey);

        await harness.ForwardAsync(firstParallel, BusinessPayload("parallel-one"), Success());

        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        var secondParallel = harness.Dispatcher.Commands[1];
        Assert.Equal("task.parallel.two", secondParallel.TaskKey);

        await harness.ForwardAsync(secondParallel, BusinessPayload("parallel-two"), Success());

        Assert.Equal(3, harness.Dispatcher.Commands.Count);
        var afterParallel = harness.Dispatcher.Commands[2];
        Assert.Equal("task.after.parallel.limit", afterParallel.TaskKey);

        await harness.ForwardAsync(afterParallel, BusinessPayload("after-parallel-limit"), Success());

        var instance = await harness.GetInstanceAsync(firstParallel);
        Assert.Equal(OrchestrationInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public async Task EngineIgnoresLateParallelCallbackAfterInstanceFailed()
    {
        var groupId = Id.New();

        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            StageWithGraph(
                Id.New(),
                "stage-parallel",
                1,
                [new ParallelGroupArtifact(groupId, ParallelJoinPolicy.WaitAll, null)],
                [],
                MessagingTask("task.parallel.fail", 1, executionMode: TaskExecutionMode.Parallel, parallelGroupId: groupId),
                MessagingTask("task.parallel.late", 2, executionMode: TaskExecutionMode.Parallel, parallelGroupId: groupId))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-parallel-late");

        var failedCommand = harness.Dispatcher.Commands.Single(command => command.TaskKey == "task.parallel.fail");
        var lateCommand = harness.Dispatcher.Commands.Single(command => command.TaskKey == "task.parallel.late");

        await harness.ForwardAsync(failedCommand, null, Failure("PermanentFailure", "parallel task failed"));
        await harness.ForwardAsync(lateCommand, BusinessPayload("late-success"), Success());

        var instance = await harness.GetInstanceAsync(failedCommand);
        var lateTask = await harness.GetTaskAsync(lateCommand);
        var transitions = await harness.GetTransitionsAsync(failedCommand);

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal(TaskExecutionStatus.WaitingResponse, lateTask.Status);
        Assert.DoesNotContain(transitions, transition =>
            transition.TaskExecutionId == lateTask.Id &&
            transition.TransitionType == "TaskCallbackCompleted");
    }

    [Fact]
    public async Task EngineSkipsCompensationWhenCompensationConditionIsFalse()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.completed",
                1,
                compensation: Compensation("task.completed.undo", EnabledCondition("false")))),
            Stage("stage-two", 2, MessagingTask(
                "task.failing",
                1,
                onErrorPolicy: OnErrorPolicy.StopAndCompensate))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-compensation-skip");

        var completedCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(completedCommand, BusinessPayload("completed-response"), Success());

        var failingCommand = harness.Dispatcher.Commands[1];
        await harness.ForwardAsync(failingCommand, null, Failure("PermanentFailure", "requires compensation"));

        var instance = await harness.GetInstanceAsync(completedCommand);
        var compensations = await harness.GetCompensationsAsync(completedCommand);
        var transitions = await harness.GetTransitionsAsync(completedCommand);

        Assert.Equal(OrchestrationInstanceStatus.Compensated, instance.Status);
        Assert.Equal(2, harness.Dispatcher.Commands.Count);
        Assert.Equal("Skipped", compensations.Single().Status);
        Assert.Contains(transitions, transition => transition.TransitionType == "CompensationSkipped");
    }

    [Fact]
    public async Task EngineFailsCompensationWhenCompensationConditionCannotBeEvaluated()
    {
        using var harness = await MessagingEngineHarness.CreateAsync(CreateArtifact(
            Stage("stage-one", 1, MessagingTask(
                "task.completed",
                1,
                compensation: Compensation("task.completed.undo", EnabledCondition("$trigger.value == 'allowed'")))),
            Stage("stage-two", 2, MessagingTask(
                "task.failing",
                1,
                onErrorPolicy: OnErrorPolicy.StopAndCompensate))));

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-compensation-condition-failure");

        var completedCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(completedCommand, BusinessPayload("completed-response"), Success());

        var failingCommand = harness.Dispatcher.Commands[1];
        await harness.ForwardAsync(failingCommand, null, Failure("PermanentFailure", "requires compensation"));

        var instance = await harness.GetInstanceAsync(completedCommand);
        var compensations = await harness.GetCompensationsAsync(completedCommand);
        var transitions = await harness.GetTransitionsAsync(completedCommand);
        var compensation = compensations.Single();

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal("Failed", compensation.Status);
        Assert.Equal("ConditionAdapterNotConfigured", compensation.Metadata["ConditionErrorCode"]!.GetValue<string>());
        Assert.True(instance.Metadata["Compensation.TerminalFailure"]!.GetValue<bool>());
        Assert.DoesNotContain(harness.Dispatcher.Commands, command => command.TaskKey == "task.completed" && !command.AwaitResponse);
        Assert.Contains(transitions, transition => transition.TransitionType == "CompensationConditionFailed");
    }

    [Fact]
    public async Task EngineFailsWhenCompensationDispatchFails()
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

        await harness.StartAsync(BusinessPayload("trigger"), "correlation-compensation-failure");

        var completedCommand = harness.Dispatcher.Commands.Single();
        await harness.ForwardAsync(completedCommand, BusinessPayload("completed-response"), Success());

        harness.Dispatcher.FailNextMatching(
            command => command.TaskKey == "task.completed" && !command.AwaitResponse,
            new TimeoutException("compensation broker unavailable"));
        var failingCommand = harness.Dispatcher.Commands[1];
        await harness.ForwardAsync(failingCommand, null, Failure("PermanentFailure", "requires compensation"));

        var instance = await harness.GetInstanceAsync(completedCommand);
        var compensations = await harness.GetCompensationsAsync(completedCommand);
        var transitions = await harness.GetTransitionsAsync(completedCommand);

        Assert.Equal(OrchestrationInstanceStatus.Failed, instance.Status);
        Assert.Equal("Failed", compensations.Single().Status);
        Assert.Equal("compensation broker unavailable", compensations.Single().ErrorMessage);
        Assert.Contains(transitions, transition => transition.TransitionType == "CompensationFailed");
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

    private static OrchestrationArtifact CreateArtifact(
        IReadOnlyList<TriggerBindingArtifact> triggers,
        params StageArtifact[] stages)
        => new(
            Id.New(),
            Id.New(),
            "test.orchestration",
            "Test Orchestration",
            "test",
            MessagingVersion,
            new Checksum($"messaging-decision-e2e-{Guid.NewGuid():N}"),
            triggers,
            [],
            stages);

    private static TriggerBindingArtifact EventTrigger(bool validationEnabled)
    {
        var schemaBinding = new SchemaBindingArtifact(
            Id.New(),
            ElementType.Orchestration,
            Id.New(),
            Id.New(),
            "events.test.trigger",
            MessagingVersion,
            Id.New(),
            true)
        {
            ContractKind = SchemaContractKind.Event,
            IsValidationEnabled = false
        };
        var channel = new EventTriggerChannelArtifact(
            schemaBinding,
            "events.test.trigger",
            MessagingVersion)
        {
            Validation = new ValidationArtifact(
                EngineType.DSL,
                new DslValidationConfigurationArtifact
                {
                    Dsl = "trigger payload validation"
                })
            {
                IsEnabled = validationEnabled,
                ErrorCode = "TriggerInvalid"
            }
        };

        return new TriggerBindingArtifact(
            Id.New(),
            TriggerType.Event,
            channel,
            true,
            "Test event trigger");
    }

    private static StageArtifact Stage(string key, int order, params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, ExecutionCondition(), tasks, [], []);

    private static StageArtifact Stage(
        string key,
        int order,
        ExecutionConditionArtifact condition,
        params TaskArtifact[] tasks)
        => new(Id.New(), key, key, order, condition, tasks, [], []);

    private static StageArtifact StageWithGraph(
        Id id,
        string key,
        int order,
        IReadOnlyList<ParallelGroupArtifact> parallelGroups,
        IReadOnlyList<BranchRuleArtifact> branchRules,
        params TaskArtifact[] tasks)
        => new(id, key, key, order, ExecutionCondition(), tasks, parallelGroups, branchRules);

    private static TaskArtifact MessagingTask(
        string key,
        int order,
        RetryPolicyArtifact? retryPolicy = null,
        TimeoutPolicyArtifact? timeoutPolicy = null,
        OnErrorPolicy onErrorPolicy = OnErrorPolicy.Stop,
        CompensationArtifact? compensation = null,
        TaskDispatchType dispatchType = TaskDispatchType.FireAndWaitCallback,
        TaskExecutionMode executionMode = TaskExecutionMode.Sequential,
        Id? parallelGroupId = null,
        ExecutionConditionArtifact? executionCondition = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Messaging,
            executionMode,
            parallelGroupId,
            executionCondition ?? ExecutionCondition(),
            Transformation(),
            new MessagingTaskConfigurationArtifact(key, MessagingVersion, null!),
            retryPolicy,
            timeoutPolicy,
            onErrorPolicy,
            compensation,
            dispatchType,
            true);

    private static TaskArtifact MessagingTaskWithResponseValidation(
        string key,
        int order)
    {
        var responseBinding = new SchemaBindingArtifact(
            Id.New(),
            ElementType.Task,
            Id.New(),
            Id.New(),
            $"{key}.reply",
            MessagingVersion,
            Id.New(),
            true)
        {
            ContractKind = SchemaContractKind.CommandResponse,
            IsValidationEnabled = false
        };
        var configuration = new MessagingTaskConfigurationArtifact(key, MessagingVersion, null!)
        {
            ResponseSchemaBinding = responseBinding,
            ResponseValidation = new ValidationArtifact(
                EngineType.DSL,
                new DslValidationConfigurationArtifact { Dsl = "response validation dsl" })
            {
                IsEnabled = true,
                ErrorCode = "ResponseShapeInvalid"
            }
        };

        return new TaskArtifact(
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
            configuration,
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);
    }

    private static TaskArtifact HttpTask(
        string key,
        int order,
        RetryPolicyArtifact? retryPolicy = null)
        => new(
            Id.New(),
            key,
            key,
            order,
            string.Empty,
            TaskKind.Http,
            TaskExecutionMode.Sequential,
            null,
            ExecutionCondition(),
            Transformation(),
            null!,
            retryPolicy,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static TaskArtifact MisconfiguredMessagingTask(
        string key,
        int order,
        RetryPolicyArtifact retryPolicy)
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
            null!,
            retryPolicy,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static TransformationArtifact Transformation()
        => new(EngineType.DSL, new DslTransformationConfigurationArtifact());

    private static ExecutionConditionArtifact ExecutionCondition()
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression("true")));

    private static ExecutionConditionArtifact EnabledCondition(string expression)
        => new(EngineType.DSL, new DslConditionConfigurationArtifact(new Expression(expression)))
        {
            IsEnabled = true
        };

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

    private static CompensationArtifact Compensation(
        string topic,
        ExecutionConditionArtifact? executionCondition = null)
        => new(
            TaskKind.Messaging,
            Transformation(),
            executionCondition ?? ExecutionCondition(),
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

    private sealed class EmptyIngressConfigurationAccessor : IGetIngressConfigurationByArtifactAccessor
    {
        public Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
            string artifactId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<IngressConfiguration>>(Array.Empty<IngressConfiguration>());
    }
}
