using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using NSubstitute;
using DesignPagedResult = Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage.PagedResult<Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationDefinition>;
using SecurityPagedResult = Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage.PagedResult<Krackend.Sagas.Orchestrations.ControlPlane.Security.Core.Team>;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class ControlPlaneApplicationHandlersTests
{
    [Fact]
    public async Task CreateOrchestrationDefinitionHandlerCreatesDefinitionWithActiveDomainAndTeam()
    {
        var definitionRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        OrchestrationDefinition? created = null;
        definitionRepository
            .Create(Arg.Do<OrchestrationDefinition>(model => created = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow
        };
        var team = new Team
        {
            Id = Id.New(),
            Key = "sales-team",
            DisplayName = "Sales Team",
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow
        };
        var domainRepository = Substitute.For<IDomainRepository>();
        domainRepository.GetById(domain.Id, Arg.Any<CancellationToken>()).Returns(domain);
        var teamRepository = Substitute.For<ITeamRepository>();
        teamRepository.GetById(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        var handler = new CreateOrchestrationDefinitionCommandHandler(definitionRepository, domainRepository, teamRepository);

        var result = await handler.Handle(
            new CreateOrchestrationDefinitionCommand(
                "sales.sale.created",
                "Sale Created",
                domain.Id.ToString(),
                "Happy path",
                team.Id.ToString(),
                ["sales", "saga", "sales"],
                "operator"),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(created.Id.ToString(), result);
        Assert.Equal("sales.sale.created", created.Key);
        Assert.Equal("Sales", created.Domain);
        Assert.Equal(domain.Id, created.DomainId);
        Assert.Equal("Sales Team", created.OwnerTeam);
        Assert.Equal(team.Id, created.OwnerTeamId);
        Assert.Equal(["sales", "saga"], created.Tags);
        Assert.True(created.IsActive);
        Assert.Equal("operator", created.CreatedBy);
    }

    [Fact]
    public async Task CreateOrchestrationDefinitionHandlerRejectsDisabledDomainOrTeam()
    {
        var definitionRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var domainRepository = Substitute.For<IDomainRepository>();
        var teamRepository = Substitute.For<ITeamRepository>();
        var domain = new Domain { Id = Id.New(), Key = "sales", DisplayName = "Sales", IsActive = false };
        var team = new Team { Id = Id.New(), Key = "sales-team", DisplayName = "Sales Team", IsActive = false };
        domainRepository.GetById(domain.Id, Arg.Any<CancellationToken>()).Returns(domain);
        teamRepository.GetById(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        var handler = new CreateOrchestrationDefinitionCommandHandler(definitionRepository, domainRepository, teamRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateOrchestrationDefinitionCommand("key", "name", domain.Id.ToString(), "", team.Id.ToString(), [], "operator"),
            CancellationToken.None));

        domain.IsActive = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateOrchestrationDefinitionCommand("key", "name", domain.Id.ToString(), "", team.Id.ToString(), [], "operator"),
            CancellationToken.None));

        await definitionRepository.DidNotReceiveWithAnyArgs().Create(default!, default);
    }

    [Fact]
    public async Task CreateTaskDefinitionHandlerCreatesTaskWithOptionalFlagsAndParallelGroup()
    {
        var repository = Substitute.For<ITaskRepository>();
        TaskDefinition? created = null;
        repository.Create(Arg.Do<TaskDefinition>(model => created = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new CreateTaskDefinitionCommandHandler(repository);
        var stageId = Id.New();
        var groupId = Id.New();
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("$trigger.Total > 0") },
        };
        var transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = "map request" },
        };
        var configuration = new MessagingTaskConfiguration
        {
            Topic = "inventories.reserve",
            Version = new SemanticVersion(1, 0, 0),
        };

        var result = await handler.Handle(
            new CreateTaskDefinitionCommand(
                stageId.ToString(),
                "inventories.reserve",
                "Reserve inventory",
                3,
                "notes",
                TaskKind.Messaging,
                TaskExecutionMode.Parallel,
                groupId.ToString(),
                condition,
                transformation,
                configuration,
                null!,
                null!,
                OnErrorPolicy.StopAndCompensate,
                null!,
                TaskDispatchType.FireAndWaitCallback,
                true),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Equal(created.Id.ToString(), result);
        Assert.Equal(stageId, created.StageDefinitionId);
        Assert.Equal(groupId, created.ParallelGroupId);
        Assert.True(created.HasExecutionCondition);
        Assert.Same(condition, created.ExecutionCondition);
        Assert.True(created.HasTransformation);
        Assert.Same(transformation, created.Transformation);
        Assert.Same(configuration, created.Configuration);
        Assert.Equal(OnErrorPolicy.StopAndCompensate, created.OnErrorPolicy);
        Assert.Equal(TaskDispatchType.FireAndWaitCallback, created.DispatchType);
    }

    [Fact]
    public async Task CreateTaskDefinitionHandlerCreatesTaskWithoutOptionalPipelineConfiguration()
    {
        var repository = Substitute.For<ITaskRepository>();
        TaskDefinition? created = null;
        repository.Create(Arg.Do<TaskDefinition>(model => created = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new CreateTaskDefinitionCommandHandler(repository);
        var stageId = Id.New();

        await handler.Handle(
            new CreateTaskDefinitionCommand(
                stageId.ToString(),
                "approval",
                "Approval",
                1,
                "",
                TaskKind.HumanApproval,
                TaskExecutionMode.Sequential,
                "",
                null!,
                null!,
                new HumanApprovalTaskConfiguration(),
                null!,
                null!,
                OnErrorPolicy.Stop,
                null!,
                TaskDispatchType.FireAndForget,
                false),
            CancellationToken.None);

        Assert.NotNull(created);
        Assert.Null(created.ParallelGroupId);
        Assert.False(created.HasExecutionCondition);
        Assert.False(created.HasTransformation);
        Assert.False(created.IsEnabled);
    }

    [Fact]
    public async Task CreateStageVariableTriggerAndParallelGroupHandlersPersistModels()
    {
        var versionId = Id.New();
        var stageId = Id.New();
        var stageRepository = Substitute.For<IStageRepository>();
        var variableRepository = Substitute.For<IVariableDefinitionRepository>();
        var triggerRepository = Substitute.For<ITriggerBindingRepository>();
        var parallelGroupRepository = Substitute.For<IParallelGroupRepository>();
        StageDefinition? createdStage = null;
        VariableDefinition? createdVariable = null;
        TriggerBinding? createdTrigger = null;
        ParallelGroupDefinition? createdGroup = null;
        stageRepository.Create(Arg.Do<StageDefinition>(model => createdStage = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        variableRepository.Create(Arg.Do<VariableDefinition>(model => createdVariable = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        triggerRepository.Create(Arg.Do<TriggerBinding>(model => createdTrigger = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        parallelGroupRepository.Create(Arg.Do<ParallelGroupDefinition>(model => createdGroup = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("$trigger.enabled == true") },
        };
        var triggerChannel = new EventTriggerChannel
        {
            Topic = "sales.sale.created",
            Version = new SemanticVersion(1, 0, 0)
        };

        var stageResult = await new CreateStageDefinitionCommandHandler(stageRepository).Handle(
            new CreateStageDefinitionCommand(
                versionId.ToString(),
                "inventory",
                "Inventory",
                "Inventory stage",
                2,
                condition),
            CancellationToken.None);
        var variableResult = await new CreateVariableDefinitionCommandHandler(variableRepository).Handle(
            new CreateVariableDefinitionCommand(
                versionId.ToString(),
                "saleTotal",
                "Sale Total",
                "Original sale total",
                VariableScope.Instance,
                VariableValueType.Decimal,
                "0",
                true,
                false),
            CancellationToken.None);
        var triggerResult = await new CreateTriggerBindingCommandHandler(triggerRepository).Handle(
            new CreateTriggerBindingCommand(
                versionId.ToString(),
                "sale-created",
                TriggerType.Event,
                triggerChannel,
                true,
                "Initial sale trigger"),
            CancellationToken.None);
        var groupResult = await new CreateParallelGroupDefinitionCommandHandler(parallelGroupRepository).Handle(
            new CreateParallelGroupDefinitionCommand(
                stageId.ToString(),
                stageId.ToString(),
                "reservation-group",
                ParallelJoinPolicy.WaitAll,
                4),
            CancellationToken.None);

        Assert.Equal(createdStage!.Id.ToString(), stageResult);
        Assert.Equal(versionId, createdStage.OrchestrationVersionId);
        Assert.Equal("inventory", createdStage.Key);
        Assert.True(createdStage.HasExecutionCondition);
        Assert.Same(condition, createdStage.ExecutionCondition);
        Assert.Equal(createdVariable!.Id.ToString(), variableResult);
        Assert.Equal(VariableScope.Instance, createdVariable.Scope);
        Assert.Equal(VariableValueType.Decimal, createdVariable.ValueType);
        Assert.True(createdVariable.IsRequired);
        Assert.False(createdVariable.IsSensitive);
        Assert.Equal(createdTrigger!.Id.ToString(), triggerResult);
        Assert.Same(triggerChannel, createdTrigger.TriggerChannel);
        Assert.True(createdTrigger.IsEnabled);
        Assert.Equal(stageId.ToString(), groupResult);
        Assert.Equal(stageId, createdGroup!.StageDefinitionId);
        Assert.Equal(ParallelJoinPolicy.WaitAll, createdGroup.JoinPolicy);
        Assert.Equal(4, createdGroup.MaxParallelAgents);
    }

    [Fact]
    public async Task CreateBranchRuleHandlerAllowsOnlyForwardNavigationInsideSameVersion()
    {
        var versionId = Id.New();
        var source = Stage(versionId, "source", 1);
        var target = Stage(versionId, "target", 2);
        var branchRepository = Substitute.For<IBranchRuleRepository>();
        var stageRepository = Substitute.For<IStageRepository>();
        BranchRuleDefinition? created = null;
        branchRepository.Create(Arg.Do<BranchRuleDefinition>(model => created = model), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        stageRepository.GetById(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        stageRepository.GetById(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        var handler = new CreateBranchRuleDefinitionCommandHandler(branchRepository, stageRepository);
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("true") },
        };

        var result = await handler.Handle(
            new CreateBranchRuleDefinitionCommand(
                ElementType.Stage,
                source.Id.ToString(),
                condition,
                ElementType.Stage,
                target.Id.ToString()),
            CancellationToken.None);

        Assert.Equal(created!.Id.ToString(), result);
        Assert.Equal(source.Id, created.FromId);
        Assert.Equal(target.Id, created.NavigateToId);
        Assert.Same(condition, created.Condition);

        var backwardsTarget = Stage(versionId, "backwards", 1);
        stageRepository.GetById(backwardsTarget.Id, Arg.Any<CancellationToken>()).Returns(backwardsTarget);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateBranchRuleDefinitionCommand(
                ElementType.Stage,
                target.Id.ToString(),
                condition,
                ElementType.Stage,
                backwardsTarget.Id.ToString()),
            CancellationToken.None));

        var otherVersionTarget = Stage(Id.New(), "other", 3);
        stageRepository.GetById(otherVersionTarget.Id, Arg.Any<CancellationToken>()).Returns(otherVersionTarget);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateBranchRuleDefinitionCommand(
                ElementType.Stage,
                source.Id.ToString(),
                condition,
                ElementType.Stage,
                otherVersionTarget.Id.ToString()),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateHandlersPersistDefinitionChanges()
    {
        var versionId = Id.New();
        var stage = Stage(versionId, "inventory", 1);
        var variable = Variable(versionId, "oldVariable");
        var trigger = Trigger(versionId, "old-trigger", enabled: true);
        var version = Version();
        var stageRepository = Substitute.For<IStageRepository>();
        var variableRepository = Substitute.For<IVariableDefinitionRepository>();
        var triggerRepository = Substitute.For<ITriggerBindingRepository>();
        var versionRepository = Substitute.For<IOrchestrationVersionRepository>();
        stageRepository.GetById(stage.Id, Arg.Any<CancellationToken>()).Returns(stage);
        variableRepository.GetById(variable.Id, Arg.Any<CancellationToken>()).Returns(variable);
        triggerRepository.GetById(trigger.Id, Arg.Any<CancellationToken>()).Returns(trigger);
        versionRepository.GetById(version.Id, Arg.Any<CancellationToken>()).Returns(version);
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("$trigger.enabled") }
        };
        var channel = new EventTriggerChannel
        {
            Topic = "sales.sale.updated",
            Version = new SemanticVersion(1, 1, 0)
        };

        var stageUpdated = await new UpdateStageDefinitionCommandHandler(stageRepository).Handle(
            new UpdateStageDefinitionCommand(stage.Id.ToString(), "inventory-v2", "Inventory v2", "Updated", 5, condition),
            CancellationToken.None);
        var variableUpdated = await new UpdateVariableDefinitionCommandHandler(variableRepository).Handle(
            new UpdateVariableDefinitionCommand(
                variable.Id.ToString(),
                "saleTotal",
                "Sale Total",
                "Updated total",
                VariableScope.Instance,
                VariableValueType.Decimal,
                "10",
                true,
                true),
            CancellationToken.None);
        var triggerUpdated = await new UpdateTriggerBindingCommandHandler(triggerRepository).Handle(
            new UpdateTriggerBindingCommand(trigger.Id.ToString(), "sale-updated", TriggerType.Event, channel, false, "Updated trigger"),
            CancellationToken.None);
        var versionUpdated = await new UpdateOrchestrationVersionCommandHandler(versionRepository).Handle(
            new UpdateOrchestrationVersionCommand(
                version.Id.ToString(),
                "Ready",
                "Updated version",
                "checksum-updated",
                "notes",
                "operator"),
            CancellationToken.None);

        Assert.True(stageUpdated);
        Assert.True(variableUpdated);
        Assert.True(triggerUpdated);
        Assert.True(versionUpdated);
        Assert.Equal("inventory-v2", stage.Key);
        Assert.Equal(5, stage.Order);
        Assert.True(stage.HasExecutionCondition);
        Assert.Same(condition, stage.ExecutionCondition);
        Assert.Equal("saleTotal", variable.Key);
        Assert.Equal(VariableValueType.Decimal, variable.ValueType);
        Assert.True(variable.IsSensitive);
        Assert.Equal("sale-updated", trigger.Key);
        Assert.False(trigger.IsEnabled);
        Assert.Same(channel, trigger.TriggerChannel);
        Assert.Equal(new Checksum("checksum-updated"), version.Checksum);
        Assert.Equal("operator", version.UpdatedBy);
        await stageRepository.Received(1).Update(stage, Arg.Any<CancellationToken>());
        await variableRepository.Received(1).Update(variable, Arg.Any<CancellationToken>());
        await triggerRepository.Received(1).Update(trigger, Arg.Any<CancellationToken>());
        await versionRepository.Received(1).Update(version, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateBranchRuleHandlerAllowsOnlyForwardNavigationInsideSameVersion()
    {
        var versionId = Id.New();
        var source = Stage(versionId, "source", 1);
        var target = Stage(versionId, "target", 2);
        var current = Branch(source.Id);
        var branchRepository = Substitute.For<IBranchRuleRepository>();
        var stageRepository = Substitute.For<IStageRepository>();
        branchRepository.GetById(current.Id, Arg.Any<CancellationToken>()).Returns(current);
        stageRepository.GetById(source.Id, Arg.Any<CancellationToken>()).Returns(source);
        stageRepository.GetById(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        var handler = new UpdateBranchRuleDefinitionCommandHandler(branchRepository, stageRepository);
        var condition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("$trigger.route") }
        };

        var result = await handler.Handle(
            new UpdateBranchRuleDefinitionCommand(
                current.Id.ToString(),
                ElementType.Stage,
                source.Id.ToString(),
                condition,
                ElementType.Stage,
                target.Id.ToString()),
            CancellationToken.None);

        Assert.True(result);
        Assert.Equal(target.Id, current.NavigateToId);
        Assert.Same(condition, current.Condition);
        await branchRepository.Received(1).Update(current, Arg.Any<CancellationToken>());

        var backwardsTarget = Stage(versionId, "backwards", 1);
        stageRepository.GetById(backwardsTarget.Id, Arg.Any<CancellationToken>()).Returns(backwardsTarget);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateBranchRuleDefinitionCommand(
                current.Id.ToString(),
                ElementType.Stage,
                target.Id.ToString(),
                condition,
                ElementType.Stage,
                backwardsTarget.Id.ToString()),
            CancellationToken.None));
    }

    [Fact]
    public async Task VersionApprovalHandlersApplyStatusTransitions()
    {
        var repository = Substitute.For<IOrchestrationVersionRepository>();
        var policy = new OrchestrationVersionTransitionPolicy();
        var version = Version();
        version.Status = OrchestrationVersionStatus.InReview;
        repository.GetById(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        var approved = await new ApproveOrchestrationVersionCommandHandler(repository, policy).Handle(
            new ApproveOrchestrationVersionCommand(version.Id.ToString(), "approver"),
            CancellationToken.None);
        var reopened = await new ReopenOrchestrationVersionReviewCommandHandler(repository, policy).Handle(
            new ReopenOrchestrationVersionReviewCommand(version.Id.ToString(), "reviewer"),
            CancellationToken.None);

        Assert.True(approved);
        Assert.True(reopened);
        Assert.Equal(OrchestrationVersionStatus.InReview, version.Status);
        Assert.Equal(default, version.ApprovedOnUtc);
        Assert.Equal(string.Empty, version.ApprovedBy);
        Assert.Equal("reviewer", version.UpdatedBy);
        await repository.Received(2).Update(version, Arg.Any<CancellationToken>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReopenOrchestrationVersionReviewCommandHandler(repository, policy).Handle(
            new ReopenOrchestrationVersionReviewCommand(version.Id.ToString(), "reviewer"),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetDomainsQueryHandlerMapsPagedSettingsFiltersAndSorts()
    {
        var repository = Substitute.For<IDomainRepository>();
        var mapper = new DomainApplicationMapper();
        PagedSettings? captured = null;
        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            Description = "Sales domain",
            IsActive = true
        };
        repository
            .GetAll(
                Arg.Do<PagedSettings>(settings => captured = settings),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage.PagedResult<Domain>(2, 3, 11, 5, [domain]));
        var handler = new GetDomainsQueryHandler(repository, mapper);

        var result = await handler.Handle(
            new GetDomainsQuery(
                new Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings
                {
                    PageNumber = 2,
                    PageSize = 5,
                    Filters = [new ApplicationFilter("key", "contains", "sale")],
                    Sorts = [new ApplicationSort("displayName", true)]
                },
                "sale"),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.PageNumber);
        Assert.Equal(5, captured.PageSize);
        Assert.Equal("key", captured.Filters.Single().Field);
        Assert.Equal("displayName", captured.Sorts.Single().Field);
        Assert.True(captured.Sorts.Single().Descending);
        Assert.Equal(11, result.TotalRows);
        Assert.Equal("sales", result.Rows.Single().Key);
    }

    [Fact]
    public void DefinitionMappersProjectStageTaskVariableTriggerParallelGroupAndBranchRuleModels()
    {
        var versionId = Id.New();
        var stage = Stage(versionId, "inventory", 1);
        stage.Description = null;
        stage.ExecutionCondition = new ExecutionCondition
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("true") },
        };
        stage.HasExecutionCondition = true;
        var parallelGroupId = Id.New();
        var task = TaskDefinition(
            stage.Id,
            "inventories.reserve",
            2,
            enabled: true,
            configuration: new MessagingTaskConfiguration
            {
                Topic = "inventories.reserve",
                Version = new SemanticVersion(1, 0, 0)
            });
        task.Notes = null;
        task.ExecutionMode = TaskExecutionMode.Parallel;
        task.ParallelGroupId = parallelGroupId;
        task.HasTransformation = true;
        task.Transformation = new TransformationDefinition
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = "map" }
        };
        var variable = Variable(versionId, "saleTotal");
        variable.DisplayName = "Sale Total";
        variable.Description = null;
        var trigger = Trigger(versionId, "sale-created", enabled: true);
        trigger.Description = null;
        var group = ParallelGroup(stage.Id, "reservation-group");
        group.MaxParallelAgents = 3;
        var branch = Branch(stage.Id);

        var stageModel = new StageDefinitionApplicationMapper().ToModel(stage);
        var taskModel = new TaskDefinitionApplicationMapper().ToModel(task);
        var variableModel = new VariableDefinitionApplicationMapper().ToModel(variable);
        var triggerModel = new TriggerBindingApplicationMapper().ToModel(trigger);
        var groupModel = new ParallelGroupDefinitionApplicationMapper().ToModel(group);
        var branchModel = new BranchRuleDefinitionApplicationMapper().ToModel(branch);

        Assert.Equal(string.Empty, stageModel.Description);
        Assert.True(stageModel.HasExecutionCondition);
        Assert.Equal(parallelGroupId.ToString(), taskModel.ParallelGroupId);
        Assert.Equal(string.Empty, taskModel.Notes);
        Assert.True(taskModel.HasTransformation);
        Assert.Equal(string.Empty, variableModel.Description);
        Assert.Equal("Sale Total", variableModel.DisplayName);
        Assert.Equal(string.Empty, triggerModel.Description);
        Assert.True(triggerModel.IsEnabled);
        Assert.Equal(3, groupModel.MaxParallelAgents);
        Assert.Equal(branch.NavigateToId.ToString(), branchModel.NavigateToId);
    }

    [Fact]
    public void ApplicationMappersProjectDefinitionsVersionsAndPagedResults()
    {
        var domainMapper = new DomainApplicationMapper();
        var definitionMapper = new OrchestrationDefinitionApplicationMapper();
        var versionMapper = new OrchestrationVersionApplicationMapper();
        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            Description = null,
            IsActive = true
        };
        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "sales.sale.created",
            Name = "Sale Created",
            Domain = "sales",
            OwnerTeam = "sales-team",
            Tags = ["sales"],
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "operator",
        };
        var version = new OrchestrationVersion
        {
            Id = Id.New(),
            OrchestrationDefinitionId = definition.Id,
            Version = new SemanticVersion(2, 0, 0),
            Status = OrchestrationVersionStatus.Approved,
            Checksum = new Checksum("checksum"),
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "operator",
            ApprovedOnUtc = DateTime.UtcNow,
            ApprovedBy = "approver",
            TriggerBindings = [Trigger(Id.New(), "trigger", enabled: true)],
            StageDefinitions = [Stage(Id.New(), "stage", 1)],
            VariableDefinitions = [Variable(Id.New(), "one"), Variable(Id.New(), "two")],
        };

        var domainModel = domainMapper.ToModel(domain);
        var definitionModel = definitionMapper.ToModel(definition);
        var versionModel = versionMapper.ToModel(version);
        var pagedDomains = domainMapper.ToPagedModel(new Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage.PagedResult<Domain>(1, 1, 1, 20, [domain]));
        var pagedDefinitions = definitionMapper.ToPagedModel(new DesignPagedResult(2, 3, 5, 10, [definition]));
        var pagedVersions = versionMapper.ToPagedModel(new Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage.PagedResult<OrchestrationVersion>(1, 1, 1, 25, [version]));

        Assert.Equal("sales", domainModel.Key);
        Assert.Equal(string.Empty, domainModel.Description);
        Assert.True(domainModel.IsActive);
        Assert.Equal("sales", definitionModel.DomainDisplayName);
        Assert.Equal("sales-team", definitionModel.OwnerTeamDisplayName);
        Assert.Equal("2.0.0", versionModel.Version);
        Assert.Equal(1, versionModel.TriggerBindingsCount);
        Assert.Equal(1, versionModel.StageDefinitionsCount);
        Assert.Equal(2, versionModel.VariableDefinitionsCount);
        Assert.Single(pagedDomains.Rows);
        Assert.Equal(2, pagedDefinitions.PageNumber);
        Assert.Single(pagedDefinitions.Rows);
        Assert.Single(pagedVersions.Rows);
    }

    [Fact]
    public async Task GetTeamsQueryHandlerAddsMemberCounts()
    {
        var teamA = new Team { Id = Id.New(), Key = "alpha", DisplayName = "Alpha", IsActive = true };
        var teamB = new Team { Id = Id.New(), Key = "beta", DisplayName = "Beta", Description = "Beta team", IsActive = false };
        var teamRepository = Substitute.For<ITeamRepository>();
        teamRepository.GetAll(2, 10, "a", Arg.Any<CancellationToken>())
            .Returns(new SecurityPagedResult(2, 4, 31, 10, [teamA, teamB]));
        var memberRepository = Substitute.For<ITeamMemberRepository>();
        memberRepository.GetByTeam(teamA.Id, Arg.Any<CancellationToken>())
            .Returns([new TeamMember { Id = Id.New(), TeamId = teamA.Id, ExternalUserId = "u1" }]);
        memberRepository.GetByTeam(teamB.Id, Arg.Any<CancellationToken>())
            .Returns([
                new TeamMember { Id = Id.New(), TeamId = teamB.Id, ExternalUserId = "u2" },
                new TeamMember { Id = Id.New(), TeamId = teamB.Id, ExternalUserId = "u3" }
            ]);
        var handler = new GetTeamsQueryHandler(teamRepository, memberRepository);

        var result = await handler.Handle(
            new GetTeamsQuery(new Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ApplicationPagedSettings { PageNumber = 2, PageSize = 10 }, "a"),
            CancellationToken.None);

        Assert.Equal(2, result.PageNumber);
        Assert.Equal(31, result.TotalRows);
        Assert.Equal([1, 2], result.Rows.Select(x => x.MemberCount).ToArray());
        Assert.Equal(string.Empty, result.Rows.First().Description);
        Assert.False(result.Rows.Last().IsActive);
    }

    [Fact]
    public async Task SnapshotBuilderOrdersChildrenResolvesSchemasAndValidates()
    {
        var fixture = new SnapshotFixture();
        var version = Version();
        var stageB = Stage(version.Id, "payment", 2);
        var stageA = Stage(version.Id, "inventory", 1);
        var taskB = TaskDefinition(stageA.Id, "inventories.commit", 2, enabled: true, configuration: new MessagingTaskConfiguration { Topic = "inventories.commit" });
        var taskA = TaskDefinition(stageA.Id, "inventories.reserve", 1, enabled: true, configuration: new MessagingTaskConfiguration { Topic = "inventories.reserve" });
        fixture.TriggerBindingRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([Trigger(version.Id, "z-trigger", enabled: true), Trigger(version.Id, "a-trigger", enabled: true)]);
        fixture.VariableDefinitionRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([Variable(version.Id, "z-variable"), Variable(version.Id, "a-variable")]);
        fixture.StageRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([stageB, stageA]);
        fixture.TaskRepository.GetAll(stageA.Id, Arg.Any<CancellationToken>()).Returns([taskB, taskA]);
        fixture.TaskRepository.GetAll(stageB.Id, Arg.Any<CancellationToken>())
            .Returns([TaskDefinition(stageB.Id, "payments.capture", 1, enabled: true, configuration: new MessagingTaskConfiguration { Topic = "payments.capture" })]);
        fixture.ParallelGroupRepository.GetAll(stageA.Id, Arg.Any<CancellationToken>())
            .Returns([ParallelGroup(stageA.Id, "z-group"), ParallelGroup(stageA.Id, "a-group")]);
        fixture.ParallelGroupRepository.GetAll(stageB.Id, Arg.Any<CancellationToken>())
            .Returns([]);
        fixture.BranchRuleRepository.GetAll(stageA.Id, Arg.Any<CancellationToken>())
            .Returns([Branch(stageA.Id)]);
        fixture.BranchRuleRepository.GetAll(stageB.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        var snapshot = await fixture.Builder.Build(version, CancellationToken.None);

        Assert.True(fixture.SchemaResolverCalled);
        Assert.Equal(["a-trigger", "z-trigger"], snapshot.TriggerBindings.Select(x => x.Key).ToArray());
        Assert.Equal(["a-variable", "z-variable"], snapshot.VariableDefinitions.Select(x => x.Key).ToArray());
        Assert.Equal(["inventory", "payment"], snapshot.StageDefinitions.Select(x => x.Key).ToArray());
        Assert.Equal(["inventories.reserve", "inventories.commit"], snapshot.StageDefinitions[0].TaskDefinitions.Select(x => x.Key).ToArray());
        Assert.Equal(["a-group", "z-group"], snapshot.StageDefinitions[0].ParallelGroups.Select(x => x.Name).ToArray());
        Assert.Single(snapshot.StageDefinitions[0].BranchRules);
    }

    [Fact]
    public async Task SnapshotBuilderRejectsIncompleteDeployableVersions()
    {
        var version = Version();
        var fixture = new SnapshotFixture();
        fixture.TriggerBindingRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([Trigger(version.Id, "disabled", enabled: false)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Builder.Build(version, CancellationToken.None));

        fixture = new SnapshotFixture();
        fixture.TriggerBindingRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([Trigger(version.Id, "enabled", enabled: true)]);
        fixture.StageRepository.GetAll(version.Id, Arg.Any<CancellationToken>()).Returns([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Builder.Build(version, CancellationToken.None));

        fixture = new SnapshotFixture();
        var stage = Stage(version.Id, "inventory", 1);
        fixture.TriggerBindingRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([Trigger(version.Id, "enabled", enabled: true)]);
        fixture.StageRepository.GetAll(version.Id, Arg.Any<CancellationToken>()).Returns([stage]);
        fixture.TaskRepository.GetAll(stage.Id, Arg.Any<CancellationToken>())
            .Returns([TaskDefinition(stage.Id, "disabled", 1, enabled: false, configuration: new MessagingTaskConfiguration { Topic = "disabled" })]);
        fixture.ParallelGroupRepository.GetAll(stage.Id, Arg.Any<CancellationToken>()).Returns([]);
        fixture.BranchRuleRepository.GetAll(stage.Id, Arg.Any<CancellationToken>()).Returns([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Builder.Build(version, CancellationToken.None));

        fixture = new SnapshotFixture();
        fixture.TriggerBindingRepository.GetAll(version.Id, Arg.Any<CancellationToken>())
            .Returns([Trigger(version.Id, "enabled", enabled: true)]);
        fixture.StageRepository.GetAll(version.Id, Arg.Any<CancellationToken>()).Returns([stage]);
        fixture.TaskRepository.GetAll(stage.Id, Arg.Any<CancellationToken>())
            .Returns([TaskDefinition(stage.Id, "missing-config", 1, enabled: true, configuration: null!)]);
        fixture.ParallelGroupRepository.GetAll(stage.Id, Arg.Any<CancellationToken>()).Returns([]);
        fixture.BranchRuleRepository.GetAll(stage.Id, Arg.Any<CancellationToken>()).Returns([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Builder.Build(version, CancellationToken.None));
    }

    [Fact]
    public async Task DefinitionVersionDomainAndTriggerQueriesReadRepositoriesAndMapResults()
    {
        var definition = Definition();
        var version = Version();
        var domain = new Domain
        {
            Id = Id.New(),
            Key = "sales",
            DisplayName = "Sales",
            Description = "Sales domain",
            IsActive = true
        };
        var trigger = Trigger(version.Id, "sales.sale.created", enabled: true);
        var definitionRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var versionRepository = Substitute.For<IOrchestrationVersionRepository>();
        var domainRepository = Substitute.For<IDomainRepository>();
        var triggerRepository = Substitute.For<ITriggerBindingRepository>();
        PagedSettings? definitionSettings = null;
        PagedSettings? versionSettings = null;
        definitionRepository
            .GetAll(Arg.Do<PagedSettings>(settings => definitionSettings = settings), Arg.Any<CancellationToken>())
            .Returns(new DesignPagedResult(3, 7, 33, 6, [definition]));
        definitionRepository.GetById(definition.Id, Arg.Any<CancellationToken>()).Returns(definition);
        versionRepository
            .GetAll(
                definition.Id,
                Arg.Do<PagedSettings>(settings => versionSettings = settings),
                Arg.Any<CancellationToken>())
            .Returns(new Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage.PagedResult<OrchestrationVersion>(2, 4, 18, 5, [version]));
        versionRepository.GetById(version.Id, Arg.Any<CancellationToken>()).Returns(version);
        domainRepository.GetById(domain.Id, Arg.Any<CancellationToken>()).Returns(domain);
        triggerRepository.GetAll(version.Id, Arg.Any<CancellationToken>()).Returns([trigger]);
        triggerRepository.GetById(trigger.Id, Arg.Any<CancellationToken>()).Returns(trigger);

        var definitions = await new GetOrchestrationDefinitionsQueryHandler(definitionRepository, new OrchestrationDefinitionApplicationMapper())
            .Handle(
                new GetOrchestrationDefinitionsQuery(new Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings
                {
                    PageNumber = 3,
                    PageSize = 6,
                    Filters = [new ApplicationFilter("key", "contains", "sales")],
                    Sorts = [new ApplicationSort("name", false)]
                }),
                CancellationToken.None);
        var definitionById = await new GetOrchestrationDefinitionByIdQueryHandler(definitionRepository, new OrchestrationDefinitionApplicationMapper())
            .Handle(new GetOrchestrationDefinitionByIdQuery(definition.Id.ToString()), CancellationToken.None);
        var versions = await new GetOrchestrationVersionsQueryHandler(versionRepository, new OrchestrationVersionApplicationMapper())
            .Handle(
                new GetOrchestrationVersionsQuery(
                    definition.Id.ToString(),
                    new Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ApplicationPagedSettings
                    {
                        PageNumber = 2,
                        PageSize = 5,
                        Filters = [new ApplicationFilter("status", "eq", "Approved")],
                        Sorts = [new ApplicationSort("version", true)]
                    }),
                CancellationToken.None);
        var versionById = await new GetOrchestrationVersionByIdQueryHandler(versionRepository, new OrchestrationVersionApplicationMapper())
            .Handle(new GetOrchestrationVersionByIdQuery(version.Id.ToString()), CancellationToken.None);
        var domainById = await new GetDomainByIdQueryHandler(domainRepository, new DomainApplicationMapper())
            .Handle(new GetDomainByIdQuery(domain.Id.ToString()), CancellationToken.None);
        var triggers = await new GetTriggerBindingsQueryHandler(triggerRepository, new TriggerBindingApplicationMapper())
            .Handle(new GetTriggerBindingsQuery(version.Id.ToString()), CancellationToken.None);
        var triggerById = await new GetTriggerBindingByIdQueryHandler(triggerRepository, new TriggerBindingApplicationMapper())
            .Handle(new GetTriggerBindingByIdQuery(trigger.Id.ToString()), CancellationToken.None);

        Assert.NotNull(definitionSettings);
        Assert.Equal(3, definitionSettings!.PageNumber);
        Assert.Equal("sales.sale.created", definitions.Rows.Single().Key);
        Assert.Equal(definition.Id.ToString(), definitionById.Id);
        Assert.NotNull(versionSettings);
        Assert.Equal(2, versionSettings!.PageNumber);
        Assert.Equal(18, versions.TotalRows);
        Assert.Equal(version.Id.ToString(), versionById.Id);
        Assert.Equal("sales", domainById.Key);
        Assert.Equal(trigger.Id.ToString(), Assert.Single(triggers).Id);
        Assert.Equal("sales.sale.created", triggerById.Key);
    }

    [Fact]
    public async Task VersionStatusCommandsUseTransitionPolicyAndSetStatus()
    {
        var repository = Substitute.For<IOrchestrationVersionRepository>();
        var policy = new OrchestrationVersionTransitionPolicy();
        var draft = Version();
        draft.Status = OrchestrationVersionStatus.Draft;
        repository.GetById(draft.Id, Arg.Any<CancellationToken>()).Returns(draft);

        var setInReview = await new SetOrchestrationVersionInReviewCommandHandler(repository, policy)
            .Handle(new SetOrchestrationVersionInReviewCommand(draft.Id.ToString()), CancellationToken.None);
        draft.Status = OrchestrationVersionStatus.InReview;
        var returnToDraft = await new ReturnOrchestrationVersionToDraftCommandHandler(repository, policy)
            .Handle(new ReturnOrchestrationVersionToDraftCommand(draft.Id.ToString()), CancellationToken.None);

        Assert.True(setInReview);
        Assert.True(returnToDraft);
        await repository.Received(1).SetStatus(draft.Id, OrchestrationVersionStatus.InReview, Arg.Any<CancellationToken>());
        await repository.Received(1).SetStatus(draft.Id, OrchestrationVersionStatus.Draft, Arg.Any<CancellationToken>());

        draft.Status = OrchestrationVersionStatus.Approved;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SetOrchestrationVersionInReviewCommandHandler(repository, policy)
            .Handle(new SetOrchestrationVersionInReviewCommand(draft.Id.ToString()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateParallelGroupDefinitionHandlerPersistsChangesAndNormalizesNullName()
    {
        var repository = Substitute.For<IParallelGroupRepository>();
        var group = ParallelGroup(Id.New(), "old");
        repository.GetById(group.Id, Arg.Any<CancellationToken>()).Returns(group);
        var handler = new UpdateParallelGroupDefinitionCommandHandler(repository);

        var updated = await handler.Handle(
            new UpdateParallelGroupDefinitionCommand(group.Id.ToString(), "fulfillment", ParallelJoinPolicy.WaitAll, 2),
            CancellationToken.None);
        var normalized = await handler.Handle(
            new UpdateParallelGroupDefinitionCommand(group.Id.ToString(), null!, ParallelJoinPolicy.WaitAll, null),
            CancellationToken.None);

        Assert.True(updated);
        Assert.True(normalized);
        Assert.Equal(string.Empty, group.Name);
        Assert.Equal(ParallelJoinPolicy.WaitAll, group.JoinPolicy);
        Assert.Null(group.MaxParallelAgents);
        await repository.Received(2).Update(group, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SimpleMutationHandlersDelegateToRepositories()
    {
        var id = Id.New();
        var taskRepository = Substitute.For<ITaskRepository>();
        var triggerRepository = Substitute.For<ITriggerBindingRepository>();
        var stageRepository = Substitute.For<IStageRepository>();
        var variableRepository = Substitute.For<IVariableDefinitionRepository>();
        var parallelRepository = Substitute.For<IParallelGroupRepository>();
        var branchRepository = Substitute.For<IBranchRuleRepository>();
        var domainRepository = Substitute.For<IDomainRepository>();
        var definitionRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        var condition = DslCondition("true");
        var transformation = DslTransformation("map request");

        Assert.True(await new EnableTaskDefinitionCommandHandler(taskRepository)
            .Handle(new EnableTaskDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DisableTaskDefinitionCommandHandler(taskRepository)
            .Handle(new DisableTaskDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DeleteTaskDefinitionCommandHandler(taskRepository)
            .Handle(new DeleteTaskDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new SetTaskExecutionConditionCommandHandler(taskRepository)
            .Handle(new SetTaskExecutionConditionCommand(id.ToString(), condition), CancellationToken.None));
        Assert.True(await new SetTaskTransformationCommandHandler(taskRepository)
            .Handle(new SetTaskTransformationCommand(id.ToString(), transformation), CancellationToken.None));
        Assert.True(await new EnableTriggerBindingCommandHandler(triggerRepository)
            .Handle(new EnableTriggerBindingCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DisableTriggerBindingCommandHandler(triggerRepository)
            .Handle(new DisableTriggerBindingCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DeleteTriggerBindingCommandHandler(triggerRepository)
            .Handle(new DeleteTriggerBindingCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new SetStageExecutionConditionCommandHandler(stageRepository)
            .Handle(new SetStageExecutionConditionCommand(id.ToString(), condition), CancellationToken.None));
        Assert.True(await new DeleteStageDefinitionCommandHandler(stageRepository)
            .Handle(new DeleteStageDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DeleteVariableDefinitionCommandHandler(variableRepository)
            .Handle(new DeleteVariableDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DeleteParallelGroupDefinitionCommandHandler(parallelRepository)
            .Handle(new DeleteParallelGroupDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DeleteBranchRuleDefinitionCommandHandler(branchRepository)
            .Handle(new DeleteBranchRuleDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new SetDomainIsActiveCommandHandler(domainRepository)
            .Handle(new SetDomainIsActiveCommand(id.ToString(), false), CancellationToken.None));
        Assert.True(await new ActivateOrchestrationDefinitionCommandHandler(definitionRepository)
            .Handle(new ActivateOrchestrationDefinitionCommand(id.ToString()), CancellationToken.None));
        Assert.True(await new DeactivateOrchestrationDefinitionCommandHandler(definitionRepository)
            .Handle(new DeactivateOrchestrationDefinitionCommand(id.ToString()), CancellationToken.None));

        await taskRepository.Received(1).SetIsEnabled(id, true, Arg.Any<CancellationToken>());
        await taskRepository.Received(1).SetIsEnabled(id, false, Arg.Any<CancellationToken>());
        await taskRepository.Received(1).Delete(id, Arg.Any<CancellationToken>());
        await taskRepository.Received(1).SetExecutionCondition(id, condition, Arg.Any<CancellationToken>());
        await taskRepository.Received(1).SetTransformation(id, transformation, Arg.Any<CancellationToken>());
        await triggerRepository.Received(1).SetIsEnabled(id, true, Arg.Any<CancellationToken>());
        await triggerRepository.Received(1).SetIsEnabled(id, false, Arg.Any<CancellationToken>());
        await triggerRepository.Received(1).Delete(id, Arg.Any<CancellationToken>());
        await stageRepository.Received(1).SetExecutionCondition(id, condition, Arg.Any<CancellationToken>());
        await stageRepository.Received(1).Delete(id, Arg.Any<CancellationToken>());
        await variableRepository.Received(1).Delete(id, Arg.Any<CancellationToken>());
        await parallelRepository.Received(1).Delete(id, Arg.Any<CancellationToken>());
        await branchRepository.Received(1).Delete(id, Arg.Any<CancellationToken>());
        await domainRepository.Received(1).SetIsActive(id, false, Arg.Any<CancellationToken>());
        await definitionRepository.Received(1).SetIsActive(id, true, Arg.Any<CancellationToken>());
        await definitionRepository.Received(1).SetIsActive(id, false, Arg.Any<CancellationToken>());
    }

    private static ExecutionCondition DslCondition(string expression)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression(expression) }
        };

    private static TransformationDefinition DslTransformation(string dsl)
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslTransformationConfiguration { Dsl = dsl }
        };

    private static OrchestrationDefinition Definition()
        => new()
        {
            Id = Id.New(),
            Key = "sales.sale.created",
            Name = "Sale Created",
            Domain = "sales",
            DomainId = Id.New(),
            DomainDisplayName = "Sales",
            OwnerTeam = "sales-team",
            OwnerTeamId = Id.New(),
            OwnerTeamDisplayName = "Sales Team",
            Tags = ["sales"],
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
        };

    private static OrchestrationVersion Version()
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            Status = OrchestrationVersionStatus.Approved,
            Checksum = new Checksum("checksum"),
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
        };

    private static StageDefinition Stage(Id versionId, string key, int order)
        => new()
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = key,
            Name = key,
            Order = order,
        };

    private static TaskDefinition TaskDefinition(
        Id stageId,
        string key,
        int order,
        bool enabled,
        ITaskConfiguration configuration)
        => new()
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            Key = key,
            Name = key,
            Order = order,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = configuration,
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = enabled,
        };

    private static TriggerBinding Trigger(Id versionId, string key, bool enabled)
        => new()
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = key,
            TriggerType = TriggerType.Event,
            TriggerChannel = new EventTriggerChannel { Topic = key, Version = new SemanticVersion(1, 0, 0) },
            IsEnabled = enabled,
        };

    private static VariableDefinition Variable(Id versionId, string key)
        => new()
        {
            Id = Id.New(),
            OrchestrationVersionId = versionId,
            Key = key,
            Scope = VariableScope.Definition,
            ValueType = VariableValueType.String,
        };

    private static ParallelGroupDefinition ParallelGroup(Id stageId, string name)
        => new()
        {
            Id = Id.New(),
            StageDefinitionId = stageId,
            Name = name,
            JoinPolicy = ParallelJoinPolicy.WaitAll,
        };

    private static BranchRuleDefinition Branch(Id stageId)
        => new()
        {
            Id = Id.New(),
            FromType = ElementType.Stage,
            FromId = stageId,
            NavigateToType = ElementType.Stage,
            NavigateToId = Id.New(),
            Condition = new ExecutionCondition
            {
                Engine = EngineType.DSL,
                Configuration = new DslConditionConfiguration { Expression = new Expression("true") },
            },
        };

    private sealed class SnapshotFixture
    {
        public SnapshotFixture()
        {
            SchemaBindingSnapshotResolver
                .ResolveAsync(Arg.Do<OrchestrationVersion>(_ => SchemaResolverCalled = true), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Builder = new OrchestrationVersionArtifactSnapshotBuilder(
                TriggerBindingRepository,
                VariableDefinitionRepository,
                StageRepository,
                TaskRepository,
                ParallelGroupRepository,
                BranchRuleRepository,
                SchemaBindingSnapshotResolver);
        }

        public ITriggerBindingRepository TriggerBindingRepository { get; } = Substitute.For<ITriggerBindingRepository>();
        public IVariableDefinitionRepository VariableDefinitionRepository { get; } = Substitute.For<IVariableDefinitionRepository>();
        public IStageRepository StageRepository { get; } = Substitute.For<IStageRepository>();
        public ITaskRepository TaskRepository { get; } = Substitute.For<ITaskRepository>();
        public IParallelGroupRepository ParallelGroupRepository { get; } = Substitute.For<IParallelGroupRepository>();
        public IBranchRuleRepository BranchRuleRepository { get; } = Substitute.For<IBranchRuleRepository>();
        public IOrchestrationSchemaBindingSnapshotResolver SchemaBindingSnapshotResolver { get; } = Substitute.For<IOrchestrationSchemaBindingSnapshotResolver>();
        public OrchestrationVersionArtifactSnapshotBuilder Builder { get; }
        public bool SchemaResolverCalled { get; private set; }
    }
}
