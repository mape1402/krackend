using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class CreateOrchestrationVersionCommandHandlerTests
{
    [Fact]
    public async Task HandleCreatesVersionWithoutRoadmapWhenThereIsNoPreviousVersion()
    {
        var fixture = new Fixture();
        var definitionId = Id.New();
        fixture.VersionRepository
            .GetLatestByOrchestrationDefinitionId(definitionId, Arg.Any<Id?>(), Arg.Any<CancellationToken>())
            .Returns((OrchestrationVersion)null!);

        var createdId = await fixture.Handler.Handle(Command(definitionId, "2.0.0"), CancellationToken.None);

        var created = Assert.Single(fixture.CreatedVersions);
        Assert.Equal(created.Id.ToString(), createdId);
        Assert.Equal(definitionId, created.OrchestrationDefinitionId);
        Assert.Equal("2.0.0", created.Version.ToString());
        Assert.Equal(OrchestrationVersionStatus.Draft, created.Status);
        Assert.Equal("Version 2", created.VersionLabel);
        Assert.Equal("operator", created.CreatedBy);
        Assert.Empty(fixture.CreatedStages);
        Assert.Empty(fixture.CreatedTasks);
        Assert.Empty(fixture.CreatedParallelGroups);
        Assert.Empty(fixture.CreatedBranchRules);
        Assert.Empty(fixture.CreatedTriggers);
        Assert.Empty(fixture.CreatedVariables);
        await fixture.SnapshotBuilder.DidNotReceiveWithAnyArgs().Build(default!, default);
    }

    [Fact]
    public async Task HandleClonesPreviousVersionRoadmapAndRemapsStageTaskAndGroupIds()
    {
        var fixture = new Fixture();
        var definitionId = Id.New();
        var previous = Version(definitionId, "1.0.0");
        var source = SourceSnapshot(definitionId);
        fixture.VersionRepository
            .GetLatestByOrchestrationDefinitionId(definitionId, Arg.Any<Id?>(), Arg.Any<CancellationToken>())
            .Returns(previous);
        fixture.SnapshotBuilder.Build(previous, Arg.Any<CancellationToken>()).Returns(source);

        var createdId = await fixture.Handler.Handle(Command(definitionId, "1.1.0"), CancellationToken.None);

        var targetVersionId = PrimitiveParser.ParseId(createdId);
        Assert.Equal(targetVersionId, Assert.Single(fixture.CreatedVersions).Id);
        Assert.Single(fixture.CreatedTriggers);
        Assert.Single(fixture.CreatedVariables);
        Assert.Equal(2, fixture.CreatedStages.Count);
        Assert.Equal(2, fixture.CreatedTasks.Count);
        Assert.Single(fixture.CreatedParallelGroups);
        Assert.Equal(2, fixture.CreatedBranchRules.Count);

        var clonedTrigger = fixture.CreatedTriggers.Single();
        Assert.Equal(targetVersionId, clonedTrigger.OrchestrationVersionId);
        Assert.Equal("sale-created", clonedTrigger.Key);
        Assert.Same(source.TriggerBindings[0].TriggerChannel, clonedTrigger.TriggerChannel);

        var clonedVariable = fixture.CreatedVariables.Single();
        Assert.Equal(targetVersionId, clonedVariable.OrchestrationVersionId);
        Assert.Equal("tenant", clonedVariable.Key);
        Assert.True(clonedVariable.IsRequired);

        var inventoryStage = fixture.CreatedStages.Single(x => x.Key == "inventory");
        var paymentStage = fixture.CreatedStages.Single(x => x.Key == "payment");
        Assert.Equal(targetVersionId, inventoryStage.OrchestrationVersionId);
        Assert.Equal(targetVersionId, paymentStage.OrchestrationVersionId);
        Assert.NotEqual(source.StageDefinitions[0].Id, inventoryStage.Id);
        Assert.NotEqual(source.StageDefinitions[1].Id, paymentStage.Id);
        Assert.Same(source.StageDefinitions[0].ExecutionCondition, inventoryStage.ExecutionCondition);

        var clonedGroup = fixture.CreatedParallelGroups.Single();
        Assert.Equal(inventoryStage.Id, clonedGroup.StageDefinitionId);
        Assert.NotEqual(source.StageDefinitions[0].ParallelGroups[0].Id, clonedGroup.Id);

        var reserveTask = fixture.CreatedTasks.Single(x => x.Key == "inventories.reserve");
        var chargeTask = fixture.CreatedTasks.Single(x => x.Key == "payments.charge");
        Assert.Equal(inventoryStage.Id, reserveTask.StageDefinitionId);
        Assert.Equal(paymentStage.Id, chargeTask.StageDefinitionId);
        Assert.Equal(clonedGroup.Id, reserveTask.ParallelGroupId);
        Assert.NotEqual(source.StageDefinitions[0].TaskDefinitions[0].Id, reserveTask.Id);
        Assert.Same(source.StageDefinitions[0].TaskDefinitions[0].Configuration, reserveTask.Configuration);
        Assert.Same(source.StageDefinitions[0].TaskDefinitions[0].Transformation, reserveTask.Transformation);

        var taskBranch = fixture.CreatedBranchRules.Single(x => x.FromType == ElementType.Task);
        Assert.Equal(reserveTask.Id, taskBranch.FromId);
        Assert.Equal(paymentStage.Id, taskBranch.NavigateToId);

        var stageBranch = fixture.CreatedBranchRules.Single(x => x.FromType == ElementType.Stage);
        Assert.Equal(inventoryStage.Id, stageBranch.FromId);
        Assert.Equal(chargeTask.Id, stageBranch.NavigateToId);
    }

    private static CreateOrchestrationVersionCommand Command(Id definitionId, string version)
        => new(
            definitionId.ToString(),
            version,
            OrchestrationVersionStatus.Draft,
            "Version 2",
            "Second version",
            "checksum-2",
            "notes",
            "operator");

    private static OrchestrationVersion Version(Id definitionId, string version)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionId = definitionId,
            Version = PrimitiveParser.ParseSemanticVersion(version),
            Status = OrchestrationVersionStatus.Deployed,
            VersionLabel = $"Version {version}",
            Description = "Previous",
            Checksum = new Checksum($"checksum-{version}"),
            Notes = "previous notes",
            CreatedBy = "operator"
        };

    private static OrchestrationVersion SourceSnapshot(Id definitionId)
    {
        var source = Version(definitionId, "1.0.0");
        var inventoryStageId = Id.New();
        var paymentStageId = Id.New();
        var reserveTaskId = Id.New();
        var chargeTaskId = Id.New();
        var groupId = Id.New();
        var condition = Condition("event.total > 0");
        var transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = "map request" }
        };

        source.TriggerBindings =
        [
            new TriggerBinding
            {
                Id = Id.New(),
                OrchestrationVersionId = source.Id,
                Key = "sale-created",
                TriggerType = TriggerType.Event,
                TriggerChannel = new EventTriggerChannel
                {
                    Topic = "events.sales.sale.created",
                    Version = new SemanticVersion(1, 0, 0),
                    HasValidation = true
                },
                IsEnabled = true,
                Description = "Sale created trigger"
            }
        ];
        source.VariableDefinitions =
        [
            new VariableDefinition
            {
                Id = Id.New(),
                OrchestrationVersionId = source.Id,
                Key = "tenant",
                DisplayName = "Tenant",
                Description = "Tenant id",
                Scope = VariableScope.Instance,
                ValueType = VariableValueType.String,
                DefaultValue = "default",
                IsRequired = true,
                IsSensitive = false
            }
        ];
        source.StageDefinitions =
        [
            new StageDefinition
            {
                Id = inventoryStageId,
                OrchestrationVersionId = source.Id,
                Key = "inventory",
                Name = "Inventory",
                Description = "Reserve inventory",
                Order = 1,
                HasExecutionCondition = true,
                ExecutionCondition = condition,
                ParallelGroups =
                [
                    new ParallelGroupDefinition
                    {
                        Id = groupId,
                        StageDefinitionId = inventoryStageId,
                        Name = "reserve-group",
                        JoinPolicy = ParallelJoinPolicy.WaitAll,
                        MaxParallelAgents = 2
                    }
                ],
                TaskDefinitions =
                [
                    new TaskDefinition
                    {
                        Id = reserveTaskId,
                        StageDefinitionId = inventoryStageId,
                        Key = "inventories.reserve",
                        Name = "Reserve inventory",
                        Order = 1,
                        Notes = "reserve notes",
                        Kind = TaskKind.Messaging,
                        ExecutionMode = TaskExecutionMode.Parallel,
                        ParallelGroupId = groupId,
                        HasExecutionCondition = true,
                        ExecutionCondition = condition,
                        HasTransformation = true,
                        Transformation = transformation,
                        Configuration = MessagingConfiguration("commands.inventories.reserve"),
                        OnErrorPolicy = OnErrorPolicy.StopAndCompensate,
                        DispatchType = TaskDispatchType.FireAndWaitCallback,
                        IsEnabled = true
                    }
                ],
                BranchRules =
                [
                    new BranchRuleDefinition
                    {
                        Id = Id.New(),
                        FromType = ElementType.Task,
                        FromId = reserveTaskId,
                        Condition = Condition("reserve.ok"),
                        NavigateToType = ElementType.Stage,
                        NavigateToId = paymentStageId
                    },
                    new BranchRuleDefinition
                    {
                        Id = Id.New(),
                        FromType = ElementType.Stage,
                        FromId = inventoryStageId,
                        Condition = Condition("inventory.completed"),
                        NavigateToType = ElementType.Task,
                        NavigateToId = chargeTaskId
                    }
                ]
            },
            new StageDefinition
            {
                Id = paymentStageId,
                OrchestrationVersionId = source.Id,
                Key = "payment",
                Name = "Payment",
                Description = "Charge payment",
                Order = 2,
                TaskDefinitions =
                [
                    new TaskDefinition
                    {
                        Id = chargeTaskId,
                        StageDefinitionId = paymentStageId,
                        Key = "payments.charge",
                        Name = "Charge payment",
                        Order = 1,
                        Kind = TaskKind.Messaging,
                        ExecutionMode = TaskExecutionMode.Sequential,
                        Configuration = MessagingConfiguration("commands.payments.charge"),
                        DispatchType = TaskDispatchType.FireAndWaitCallback,
                        IsEnabled = true
                    }
                ]
            }
        ];

        return source;
    }

    private static MessagingTaskConfiguration MessagingConfiguration(string topic)
        => new()
        {
            Topic = topic,
            Version = new SemanticVersion(1, 0, 0)
        };

    private static ExecutionCondition Condition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression(expression) }
        };

    private sealed class Fixture
    {
        public Fixture()
        {
            VersionRepository.Create(Arg.Do<OrchestrationVersion>(CreatedVersions.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            StageRepository.Create(Arg.Do<StageDefinition>(CreatedStages.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TaskRepository.Create(Arg.Do<TaskDefinition>(CreatedTasks.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            ParallelGroupRepository.Create(Arg.Do<ParallelGroupDefinition>(CreatedParallelGroups.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            BranchRuleRepository.Create(Arg.Do<BranchRuleDefinition>(CreatedBranchRules.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TriggerBindingRepository.Create(Arg.Do<TriggerBinding>(CreatedTriggers.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            VariableDefinitionRepository.Create(Arg.Do<VariableDefinition>(CreatedVariables.Add), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Handler = new CreateOrchestrationVersionCommandHandler(
                VersionRepository,
                StageRepository,
                TaskRepository,
                ParallelGroupRepository,
                BranchRuleRepository,
                TriggerBindingRepository,
                VariableDefinitionRepository,
                SnapshotBuilder);
        }

        public IOrchestrationVersionRepository VersionRepository { get; } = Substitute.For<IOrchestrationVersionRepository>();

        public IStageRepository StageRepository { get; } = Substitute.For<IStageRepository>();

        public ITaskRepository TaskRepository { get; } = Substitute.For<ITaskRepository>();

        public IParallelGroupRepository ParallelGroupRepository { get; } = Substitute.For<IParallelGroupRepository>();

        public IBranchRuleRepository BranchRuleRepository { get; } = Substitute.For<IBranchRuleRepository>();

        public ITriggerBindingRepository TriggerBindingRepository { get; } = Substitute.For<ITriggerBindingRepository>();

        public IVariableDefinitionRepository VariableDefinitionRepository { get; } = Substitute.For<IVariableDefinitionRepository>();

        public IOrchestrationVersionArtifactSnapshotBuilder SnapshotBuilder { get; } =
            Substitute.For<IOrchestrationVersionArtifactSnapshotBuilder>();

        public CreateOrchestrationVersionCommandHandler Handler { get; }

        public List<OrchestrationVersion> CreatedVersions { get; } = new();

        public List<StageDefinition> CreatedStages { get; } = new();

        public List<TaskDefinition> CreatedTasks { get; } = new();

        public List<ParallelGroupDefinition> CreatedParallelGroups { get; } = new();

        public List<BranchRuleDefinition> CreatedBranchRules { get; } = new();

        public List<TriggerBinding> CreatedTriggers { get; } = new();

        public List<VariableDefinition> CreatedVariables { get; } = new();
    }
}
