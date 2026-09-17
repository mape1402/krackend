namespace Krackend.Sagas.Orchestrations.Tests.Design;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using NSubstitute;

public sealed class ControlPlaneApplicationQueryHandlersTests
{
    [Fact]
    public async Task StageQueryHandlersReadRepositoryAndMapModels()
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var repository = Substitute.For<IStageRepository>();
        var stage = new StageDefinition
        {
            Id = stageId,
            OrchestrationVersionId = versionId,
            Key = "inventory-reservation",
            Name = "Inventory reservation",
            Description = "Reserve inventory before payment.",
            Order = 2,
            HasExecutionCondition = true,
            ExecutionCondition = Condition("$trigger.Total > 0")
        };
        repository.GetAll(versionId, Arg.Any<CancellationToken>()).Returns([stage]);
        repository.GetById(stageId, Arg.Any<CancellationToken>()).Returns(stage);
        var mapper = new StageDefinitionApplicationMapper();

        var all = await new GetStageDefinitionsQueryHandler(repository, mapper)
            .Handle(new GetStageDefinitionsQuery(versionId.ToString()), CancellationToken.None);
        var single = await new GetStageDefinitionByIdQueryHandler(repository, mapper)
            .Handle(new GetStageDefinitionByIdQuery(stageId.ToString()), CancellationToken.None);

        Assert.Equal(stage.Id.ToString(), Assert.Single(all).Id);
        Assert.Equal("inventory-reservation", single.Key);
        Assert.True(single.HasExecutionCondition);
    }

    [Fact]
    public async Task TaskQueryHandlersReadRepositoryAndMapModels()
    {
        var stageId = Id.New();
        var taskId = Id.New();
        var parallelGroupId = Id.New();
        var repository = Substitute.For<ITaskRepository>();
        var task = new TaskDefinition
        {
            Id = taskId,
            StageDefinitionId = stageId,
            Key = "inventories.reserve",
            Name = "Reserve inventory",
            Notes = "notes",
            Order = 3,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Parallel,
            ParallelGroupId = parallelGroupId,
            Configuration = new MessagingTaskConfiguration
            {
                Topic = "commands.inventories.reserve",
                Version = new SemanticVersion(1, 0, 0)
            },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
            IsEnabled = true
        };
        repository.GetAll(stageId, Arg.Any<CancellationToken>()).Returns([task]);
        repository.GetById(taskId, Arg.Any<CancellationToken>()).Returns(task);
        var mapper = new TaskDefinitionApplicationMapper();

        var all = await new GetTaskDefinitionsQueryHandler(repository, mapper)
            .Handle(new GetTaskDefinitionsQuery(stageId.ToString()), CancellationToken.None);
        var single = await new GetTaskDefinitionByIdQueryHandler(repository, mapper)
            .Handle(new GetTaskDefinitionByIdQuery(taskId.ToString()), CancellationToken.None);

        Assert.Equal(task.Id.ToString(), Assert.Single(all).Id);
        Assert.Equal(parallelGroupId.ToString(), single.ParallelGroupId);
        Assert.Equal(TaskDispatchType.FireAndWaitCallback, single.DispatchType);
    }

    [Fact]
    public async Task VariableQueryHandlersReadRepositoryAndMapModels()
    {
        var versionId = Id.New();
        var variableId = Id.New();
        var repository = Substitute.For<IVariableDefinitionRepository>();
        var variable = new VariableDefinition
        {
            Id = variableId,
            OrchestrationVersionId = versionId,
            Key = "region",
            DisplayName = "Region",
            Description = "Execution region.",
            Scope = VariableScope.Environment,
            ValueType = VariableValueType.String,
            DefaultValue = "north",
            IsRequired = true,
            IsSensitive = false
        };
        repository.GetAll(versionId, Arg.Any<CancellationToken>()).Returns([variable]);
        repository.GetById(variableId, Arg.Any<CancellationToken>()).Returns(variable);
        var mapper = new VariableDefinitionApplicationMapper();

        var all = await new GetVariableDefinitionsQueryHandler(repository, mapper)
            .Handle(new GetVariableDefinitionsQuery(versionId.ToString()), CancellationToken.None);
        var single = await new GetVariableDefinitionByIdQueryHandler(repository, mapper)
            .Handle(new GetVariableDefinitionByIdQuery(variableId.ToString()), CancellationToken.None);

        Assert.Equal(variable.Id.ToString(), Assert.Single(all).Id);
        Assert.Equal("north", single.DefaultValue);
        Assert.True(single.IsRequired);
    }

    [Fact]
    public async Task ParallelGroupQueryHandlersReadRepositoryAndMapModels()
    {
        var stageId = Id.New();
        var groupId = Id.New();
        var repository = Substitute.For<IParallelGroupRepository>();
        var group = new ParallelGroupDefinition
        {
            Id = groupId,
            StageDefinitionId = stageId,
            Name = "fulfillment",
            JoinPolicy = ParallelJoinPolicy.WaitAll,
            MaxParallelAgents = 4
        };
        repository.GetAll(stageId, Arg.Any<CancellationToken>()).Returns([group]);
        repository.GetById(groupId, Arg.Any<CancellationToken>()).Returns(group);
        var mapper = new ParallelGroupDefinitionApplicationMapper();

        var all = await new GetParallelGroupDefinitionsQueryHandler(repository, mapper)
            .Handle(new GetParallelGroupDefinitionsQuery(stageId.ToString()), CancellationToken.None);
        var single = await new GetParallelGroupDefinitionByIdQueryHandler(repository, mapper)
            .Handle(new GetParallelGroupDefinitionByIdQuery(groupId.ToString()), CancellationToken.None);

        Assert.Equal(group.Id.ToString(), Assert.Single(all).Id);
        Assert.Equal(ParallelJoinPolicy.WaitAll, single.JoinPolicy);
        Assert.Equal(4, single.MaxParallelAgents);
    }

    [Fact]
    public async Task BranchRuleQueryHandlersReadRepositoryAndMapModels()
    {
        var stageId = Id.New();
        var ruleId = Id.New();
        var targetStageId = Id.New();
        var repository = Substitute.For<IBranchRuleRepository>();
        var rule = new BranchRuleDefinition
        {
            Id = ruleId,
            FromType = ElementType.Stage,
            FromId = stageId,
            Condition = Condition("$responses.inventory_reservation.inventories_reserve.Reserved == true"),
            NavigateToType = ElementType.Stage,
            NavigateToId = targetStageId
        };
        repository.GetAll(stageId, Arg.Any<CancellationToken>()).Returns([rule]);
        repository.GetById(ruleId, Arg.Any<CancellationToken>()).Returns(rule);
        var mapper = new BranchRuleDefinitionApplicationMapper();

        var all = await new GetBranchRuleDefinitionsQueryHandler(repository, mapper)
            .Handle(new GetBranchRuleDefinitionsQuery(stageId.ToString()), CancellationToken.None);
        var single = await new GetBranchRuleDefinitionByIdQueryHandler(repository, mapper)
            .Handle(new GetBranchRuleDefinitionByIdQuery(ruleId.ToString()), CancellationToken.None);

        Assert.Equal(rule.Id.ToString(), Assert.Single(all).Id);
        Assert.Equal(stageId.ToString(), single.FromId);
        Assert.Equal(targetStageId.ToString(), single.NavigateToId);
    }

    private static ExecutionCondition Condition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration
            {
                Expression = new Expression(expression)
            }
        };
}
