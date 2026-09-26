namespace Krackend.Sagas.Orchestrations.Tests.Runtime;

using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Payloads;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using NSubstitute;

public sealed class RuntimeEntryValidatorTests
{
    [Fact]
    public async Task StageEntryValidatorReturnsSuccessWhenValidationIsDisabled()
    {
        var executor = Substitute.For<IOrchestrationValidationExecutor>();
        var validator = new DefaultStageEntryValidator(executor);

        var result = await validator.ValidateAsync(CreateStage(), CreatePayloadContext());

        Assert.True(result.Succeeded);
        await executor.DidNotReceiveWithAnyArgs().ValidateAsync(default!, default);
    }

    [Fact]
    public async Task StageEntryValidatorFailsWhenValidationIsEnabledWithoutDsl()
    {
        var validator = new DefaultStageEntryValidator(Substitute.For<IOrchestrationValidationExecutor>());

        var result = await validator.ValidateAsync(
            CreateStage(new ValidationArtifact(
                EngineType.DSL,
                new DslValidationConfigurationArtifact())
            {
                IsEnabled = true,
                ErrorCode = "StageGateFailed"
            }),
            CreatePayloadContext());

        Assert.False(result.Succeeded);
        Assert.Equal("StageGateFailed", result.ErrorCode);
    }

    [Fact]
    public async Task TaskEntryValidatorDelegatesValidationWithAccumulatedContext()
    {
        var executor = Substitute.For<IOrchestrationValidationExecutor>();
        executor.ValidateAsync(Arg.Any<OrchestrationValidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationValidationResult.Success());
        var validator = new DefaultTaskEntryValidator(executor);
        var task = CreateTask(new ValidationArtifact(
            EngineType.DSL,
            new DslValidationConfigurationArtifact
            {
                Dsl = "validate task"
            })
        {
            IsEnabled = true
        });
        var payloadContext = CreatePayloadContext();

        var result = await validator.ValidateAsync(task, payloadContext);

        Assert.True(result.Succeeded);
        await executor.Received(1).ValidateAsync(
            Arg.Is<OrchestrationValidationRequest>(request =>
                request.Task == task &&
                request.PayloadContext == payloadContext &&
                request.PayloadAlias == "context" &&
                request.Phase == "TaskEntry"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TaskEntryValidatorUsesConfiguredErrorCodeWhenAdapterFails()
    {
        var executor = Substitute.For<IOrchestrationValidationExecutor>();
        executor.ValidateAsync(Arg.Any<OrchestrationValidationRequest>(), Arg.Any<CancellationToken>())
            .Returns(OrchestrationValidationResult.Failure(string.Empty, "validation failed"));
        var validator = new DefaultTaskEntryValidator(executor);

        var result = await validator.ValidateAsync(
            CreateTask(new ValidationArtifact(
                EngineType.DSL,
                new DslValidationConfigurationArtifact
                {
                    Dsl = "validate task"
                })
            {
                IsEnabled = true,
                ErrorCode = "TaskGateFailed"
            }),
            CreatePayloadContext());

        Assert.False(result.Succeeded);
        Assert.Equal("TaskGateFailed", result.ErrorCode);
        Assert.Equal("validation failed", result.ErrorMessage);
    }

    private static StageArtifact CreateStage(ValidationArtifact? validation = null)
    {
        var stage = new StageArtifact(
            Id.New(),
            "fulfillment",
            "Fulfillment",
            1,
            null,
            Array.Empty<TaskArtifact>(),
            Array.Empty<ParallelGroupArtifact>(),
            Array.Empty<BranchRuleArtifact>());

        return validation is null ? stage : stage with { EntryValidation = validation };
    }

    private static TaskArtifact CreateTask(ValidationArtifact? validation = null)
    {
        var task = new TaskArtifact(
            Id.New(),
            "inventory_reservation",
            "Inventory reservation",
            1,
            string.Empty,
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            null,
            null,
            null,
            new MessagingTaskConfigurationArtifact(
                "commands.inventory.reserve",
                new SemanticVersion(1, 0, 0),
                null),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

        return validation is null ? task : task with { EntryValidation = validation };
    }

    private static OrchestrationPayloadContext CreatePayloadContext()
        => new()
        {
            ContextPayload = JsonNode.Parse("""{"trigger":{"payload":{"saleId":"sale-1"}}}"""),
            TriggerPayload = JsonNode.Parse("""{"saleId":"sale-1"}"""),
            StageKey = "fulfillment",
            TaskKey = "inventory_reservation"
        };
}
