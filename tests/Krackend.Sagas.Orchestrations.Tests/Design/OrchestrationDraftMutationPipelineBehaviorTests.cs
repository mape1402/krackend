using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Pelican.Mediator;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class OrchestrationDraftMutationPipelineBehaviorTests
{
    private const string VersionId = "01JMJGBJ0R7WFN9QBG3CCBEVM1";
    private const string StageId = "01JMJGBJ0R7WFN9QBG3CCBEVM2";
    private const string TaskId = "01JMJGBJ0R7WFN9QBG3CCBEVM3";
    private const string TriggerId = "01JMJGBJ0R7WFN9QBG3CCBEVM4";
    private const string VariableId = "01JMJGBJ0R7WFN9QBG3CCBEVM5";
    private const string GroupId = "01JMJGBJ0R7WFN9QBG3CCBEVM6";
    private const string BranchId = "01JMJGBJ0R7WFN9QBG3CCBEVM7";

    [Fact]
    public async Task HandleGuardsStringReturningDraftMutationsBeforeCallingNext()
    {
        await AssertStringGuarded(
            new CreateStageDefinitionCommand(VersionId, "stage", "Stage", "", 0, Condition()),
            guard => guard.Received(1).EnsureVersionIsDraft(VersionId, Arg.Any<CancellationToken>()));
        await AssertStringGuarded(
            new CreateTriggerBindingCommand(VersionId, "sale.created", TriggerType.Event, EventChannel(), true, ""),
            guard => guard.Received(1).EnsureVersionIsDraft(VersionId, Arg.Any<CancellationToken>()));
        await AssertStringGuarded(
            new CreateVariableDefinitionCommand(VersionId, "saleId", "Sale Id", "", VariableScope.Instance, VariableValueType.String, "", true, false),
            guard => guard.Received(1).EnsureVersionIsDraft(VersionId, Arg.Any<CancellationToken>()));
        await AssertStringGuarded(
            TaskCreate(),
            guard => guard.Received(1).EnsureStageVersionIsDraft(StageId, Arg.Any<CancellationToken>()));
        await AssertStringGuarded(
            new CreateParallelGroupDefinitionCommand("", StageId, "Group1", ParallelJoinPolicy.WaitAll, null),
            guard => guard.Received(1).EnsureStageVersionIsDraft(StageId, Arg.Any<CancellationToken>()));
        await AssertStringGuarded(
            new CreateBranchRuleDefinitionCommand(ElementType.Task, TaskId, Condition(), ElementType.Stage, StageId),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertStringGuarded(
            new CreateBranchRuleDefinitionCommand(ElementType.Stage, StageId, Condition(), ElementType.Stage, VersionId),
            guard => guard.Received(1).EnsureStageVersionIsDraft(StageId, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task HandleGuardsBooleanReturningDraftMutationsBeforeCallingNext()
    {
        await AssertBooleanGuarded(
            new UpdateOrchestrationVersionCommand(VersionId, "v1", "", "", "", "tester"),
            guard => guard.Received(1).EnsureVersionIsDraft(VersionId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new UpdateStageDefinitionCommand(StageId, "stage", "Stage", "", 0, Condition()),
            guard => guard.Received(1).EnsureStageVersionIsDraft(StageId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DeleteStageDefinitionCommand(StageId),
            guard => guard.Received(1).EnsureStageVersionIsDraft(StageId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new SetStageExecutionConditionCommand(StageId, Condition()),
            guard => guard.Received(1).EnsureStageVersionIsDraft(StageId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new UpdateTriggerBindingCommand(TriggerId, "sale.created", TriggerType.Event, EventChannel(), true, ""),
            guard => guard.Received(1).EnsureTriggerVersionIsDraft(TriggerId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DeleteTriggerBindingCommand(TriggerId),
            guard => guard.Received(1).EnsureTriggerVersionIsDraft(TriggerId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new EnableTriggerBindingCommand(TriggerId),
            guard => guard.Received(1).EnsureTriggerVersionIsDraft(TriggerId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DisableTriggerBindingCommand(TriggerId),
            guard => guard.Received(1).EnsureTriggerVersionIsDraft(TriggerId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new UpdateVariableDefinitionCommand(VariableId, "saleId", "Sale Id", "", VariableScope.Instance, VariableValueType.String, "", true, false),
            guard => guard.Received(1).EnsureVariableVersionIsDraft(VariableId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DeleteVariableDefinitionCommand(VariableId),
            guard => guard.Received(1).EnsureVariableVersionIsDraft(VariableId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            TaskUpdate(),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DeleteTaskDefinitionCommand(TaskId),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new EnableTaskDefinitionCommand(TaskId),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DisableTaskDefinitionCommand(TaskId),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new SetTaskExecutionConditionCommand(TaskId, Condition()),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new SetTaskTransformationCommand(TaskId, Transformation()),
            guard => guard.Received(1).EnsureTaskVersionIsDraft(TaskId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new UpdateParallelGroupDefinitionCommand(GroupId, "Group", ParallelJoinPolicy.WaitAll, 2),
            guard => guard.Received(1).EnsureParallelGroupVersionIsDraft(GroupId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DeleteParallelGroupDefinitionCommand(GroupId),
            guard => guard.Received(1).EnsureParallelGroupVersionIsDraft(GroupId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new UpdateBranchRuleDefinitionCommand(BranchId, ElementType.Task, TaskId, Condition(), ElementType.Stage, StageId),
            guard => guard.Received(1).EnsureBranchRuleVersionIsDraft(BranchId, Arg.Any<CancellationToken>()));
        await AssertBooleanGuarded(
            new DeleteBranchRuleDefinitionCommand(BranchId),
            guard => guard.Received(1).EnsureBranchRuleVersionIsDraft(BranchId, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task HandleDoesNotGuardUnmatchedRequests()
    {
        var guard = Substitute.For<IOrchestrationVersionEditGuard>();
        var behavior = new OrchestrationDraftMutationPipelineBehavior<SetDomainIsActiveCommand, bool>(guard);
        var nextCalled = false;

        var result = await behavior.Handle(
            new SetDomainIsActiveCommand(VersionId, true),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.True(result);
        Assert.True(nextCalled);
        await guard.DidNotReceiveWithAnyArgs().EnsureVersionIsDraft(default!, default);
        await guard.DidNotReceiveWithAnyArgs().EnsureStageVersionIsDraft(default!, default);
        await guard.DidNotReceiveWithAnyArgs().EnsureTaskVersionIsDraft(default!, default);
    }

    private static async Task AssertStringGuarded<TRequest>(
        TRequest request,
        Func<IOrchestrationVersionEditGuard, Task> assertGuard)
        where TRequest : IRequest<string>
    {
        var guard = Substitute.For<IOrchestrationVersionEditGuard>();
        var behavior = new OrchestrationDraftMutationPipelineBehavior<TRequest, string>(guard);
        var nextCalled = false;

        var result = await behavior.Handle(
            request,
            _ =>
            {
                nextCalled = true;
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.True(nextCalled);
        await assertGuard(guard);
    }

    private static async Task AssertBooleanGuarded<TRequest>(
        TRequest request,
        Func<IOrchestrationVersionEditGuard, Task> assertGuard)
        where TRequest : IRequest<bool>
    {
        var guard = Substitute.For<IOrchestrationVersionEditGuard>();
        var behavior = new OrchestrationDraftMutationPipelineBehavior<TRequest, bool>(guard);
        var nextCalled = false;

        var result = await behavior.Handle(
            request,
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.True(result);
        Assert.True(nextCalled);
        await assertGuard(guard);
    }

    private static CreateTaskDefinitionCommand TaskCreate()
        => new(
            StageId,
            "inventories.reserve",
            "Reserve inventory",
            0,
            "",
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            "",
            Condition(),
            Transformation(),
            Messaging(),
            null!,
            null!,
            OnErrorPolicy.Stop,
            null!,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static UpdateTaskDefinitionCommand TaskUpdate()
        => new(
            TaskId,
            "inventories.reserve",
            "Reserve inventory",
            0,
            "",
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            "",
            Condition(),
            Transformation(),
            Messaging(),
            null!,
            null!,
            OnErrorPolicy.Stop,
            null!,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static ExecutionCondition Condition()
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("true") }
        };

    private static TransformationDefinition Transformation()
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = "{}" }
        };

    private static MessagingTaskConfiguration Messaging()
        => new()
        {
            Topic = "inventories.reserve",
            Version = new SemanticVersion(1, 0, 0)
        };

    private static EventTriggerChannel EventChannel()
        => new()
        {
            Topic = "events.sales.sale.created",
            Version = new SemanticVersion(1, 0, 0)
        };
}
