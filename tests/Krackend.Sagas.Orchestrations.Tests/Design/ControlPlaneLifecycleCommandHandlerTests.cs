using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using NSubstitute;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class ControlPlaneLifecycleCommandHandlerTests
{
    [Fact]
    public async Task DeployOrchestrationVersionPublishesArtifactAndUpdatesStatus()
    {
        var fixture = new LifecycleFixture(OrchestrationVersionStatus.Approved);
        OrchestrationVersionDeployedEvent? published = null;
        fixture.PublicationService
            .PublishDeployment(Arg.Do<OrchestrationVersionDeployedEvent>(evt => published = evt), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new DeployOrchestrationVersionCommandHandler(
            fixture.VersionRepository,
            fixture.DefinitionRepository,
            fixture.TransitionPolicy,
            fixture.SnapshotBuilder,
            fixture.PayloadFactory,
            fixture.DslValidationService,
            fixture.PublicationService);

        var result = await handler.Handle(new DeployOrchestrationVersionCommand(fixture.Version.Id.ToString(), "operator"), CancellationToken.None);

        Assert.True(result);
        fixture.TransitionPolicy.Received(1).EnsureCanTransition(OrchestrationVersionStatus.Approved, OrchestrationVersionStatus.Deployed);
        fixture.DslValidationService.Received(1).Validate(fixture.Version);
        await fixture.VersionRepository.Received(1).Update(fixture.Version, Arg.Any<CancellationToken>());
        Assert.Equal(OrchestrationVersionStatus.Deployed, fixture.Version.Status);
        Assert.Equal("operator", fixture.Version.UpdatedBy);
        Assert.NotNull(fixture.Version.UpdatedOnUtc);
        Assert.NotNull(published);
        Assert.Equal(fixture.Version.Id.ToString(), published.OrchestrationVersionId);
        Assert.Equal("Sale Created", published.OrchestrationDisplayName);
        Assert.Equal("1.0.0", published.VersionNumber);
        Assert.Equal("""{"artifact":true}""", published.ArtifactPayloadJson);
        Assert.Equal("operator", published.Actor);
    }

    [Fact]
    public async Task ArchiveAndDeprecateVersionPublishLifecycleArtifacts()
    {
        var archiveFixture = new LifecycleFixture(OrchestrationVersionStatus.Deprecated);
        OrchestrationVersionArchivedEvent? archived = null;
        archiveFixture.PublicationService
            .PublishArchive(Arg.Do<OrchestrationVersionArchivedEvent>(evt => archived = evt), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var archiveHandler = new ArchiveOrchestrationVersionCommandHandler(
            archiveFixture.VersionRepository,
            archiveFixture.DefinitionRepository,
            archiveFixture.TransitionPolicy,
            archiveFixture.SnapshotBuilder,
            archiveFixture.PayloadFactory,
            archiveFixture.PublicationService);

        Assert.True(await archiveHandler.Handle(new ArchiveOrchestrationVersionCommand(archiveFixture.Version.Id.ToString(), "archiver"), CancellationToken.None));
        Assert.Equal(OrchestrationVersionStatus.Archived, archiveFixture.Version.Status);
        Assert.Equal("archiver", archiveFixture.Version.UpdatedBy);
        Assert.Equal(archiveFixture.Version.Id.ToString(), archived!.CorrelationId);

        var deprecateFixture = new LifecycleFixture(OrchestrationVersionStatus.Deployed);
        OrchestrationVersionDeprecatedEvent? deprecated = null;
        deprecateFixture.PublicationService
            .PublishDeprecation(Arg.Do<OrchestrationVersionDeprecatedEvent>(evt => deprecated = evt), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var deprecateHandler = new DeprecateOrchestrationVersionCommandHandler(
            deprecateFixture.VersionRepository,
            deprecateFixture.DefinitionRepository,
            deprecateFixture.TransitionPolicy,
            deprecateFixture.SnapshotBuilder,
            deprecateFixture.PayloadFactory,
            deprecateFixture.PublicationService);

        Assert.True(await deprecateHandler.Handle(new DeprecateOrchestrationVersionCommand(deprecateFixture.Version.Id.ToString(), "deprecator"), CancellationToken.None));
        Assert.Equal(OrchestrationVersionStatus.Deprecated, deprecateFixture.Version.Status);
        Assert.Equal("deprecator", deprecateFixture.Version.UpdatedBy);
        Assert.Equal("""{"artifact":true}""", deprecated!.ArtifactPayloadJson);
    }

    [Fact]
    public async Task UpsertDomainHandlerCreatesUpdatesTrimsAndRejectsDuplicateKeys()
    {
        var repository = Substitute.For<IDomainRepository>();
        Domain? upserted = null;
        repository.Upsert(Arg.Do<Domain>(domain => upserted = domain), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new UpsertDomainCommandHandler(repository);

        var createdId = await handler.Handle(new UpsertDomainCommand("", " sales ", " Sales ", "  Domain  "), CancellationToken.None);

        Assert.NotNull(upserted);
        Assert.Equal(createdId, upserted.Id.ToString());
        Assert.Equal("sales", upserted.Key);
        Assert.Equal("Sales", upserted.DisplayName);
        Assert.Equal("Domain", upserted.Description);
        Assert.True(upserted.IsActive);
        Assert.Null(upserted.UpdatedOnUtc);

        var current = new Domain
        {
            Id = Id.New(),
            Key = "payments",
            DisplayName = "Payments",
            IsActive = false,
            CreatedOnUtc = DateTime.UtcNow.AddDays(-1)
        };
        repository.GetById(current.Id, Arg.Any<CancellationToken>()).Returns(current);
        repository.GetByKey("payments", Arg.Any<CancellationToken>()).Returns(current);

        var updatedId = await handler.Handle(new UpsertDomainCommand(current.Id.ToString(), " payments ", " Payments Updated ", null!), CancellationToken.None);

        Assert.Equal(current.Id.ToString(), updatedId);
        Assert.False(upserted!.IsActive);
        Assert.Equal("Payments Updated", upserted.DisplayName);
        Assert.NotNull(upserted.UpdatedOnUtc);

        repository.GetByKey("duplicate", Arg.Any<CancellationToken>())
            .Returns(new Domain { Id = Id.New(), Key = "duplicate", DisplayName = "Duplicate", IsActive = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpsertDomainCommand(current.Id.ToString(), "duplicate", "Duplicate", ""),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateDefinitionAndTaskHandlersMutateCurrentModels()
    {
        var definition = new OrchestrationDefinition
        {
            Id = Id.New(),
            Key = "sales.sale.created",
            Name = "Old",
            Domain = "old",
            DomainId = Id.New(),
            OwnerTeam = "old",
            OwnerTeamId = Id.New(),
            IsActive = true,
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests"
        };
        var domain = new Domain { Id = Id.New(), Key = "sales", DisplayName = "Sales", IsActive = true };
        var team = new Team { Id = Id.New(), Key = "team", DisplayName = "Team", IsActive = true };
        var definitionRepository = Substitute.For<IOrchestrationDefinitionRepository>();
        definitionRepository.GetById(definition.Id, Arg.Any<CancellationToken>()).Returns(definition);
        var domainRepository = Substitute.For<IDomainRepository>();
        domainRepository.GetById(domain.Id, Arg.Any<CancellationToken>()).Returns(domain);
        var teamRepository = Substitute.For<ITeamRepository>();
        teamRepository.GetById(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        var definitionHandler = new UpdateOrchestrationDefinitionCommandHandler(definitionRepository, domainRepository, teamRepository);

        Assert.True(await definitionHandler.Handle(
            new UpdateOrchestrationDefinitionCommand(
                definition.Id.ToString(),
                "Sale Created",
                domain.Id.ToString(),
                "Updated",
                team.Id.ToString(),
                ["sales", "sales", "happy-path"],
                "operator"),
            CancellationToken.None));

        Assert.Equal("Sale Created", definition.Name);
        Assert.Equal("Sales", definition.Domain);
        Assert.Equal("Team", definition.OwnerTeam);
        Assert.Equal(["sales", "happy-path"], definition.Tags);
        Assert.Equal("operator", definition.UpdatedBy);
        await definitionRepository.Received(1).Update(definition, Arg.Any<CancellationToken>());

        var task = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = Id.New(),
            Key = "old",
            Name = "Old",
            Order = 1,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = new MessagingTaskConfiguration { Topic = "old" },
            DispatchType = TaskDispatchType.FireAndForget,
            IsEnabled = true
        };
        var groupId = Id.New();
        var condition = new ExecutionCondition { Engine = EngineType.DSL, Configuration = new DslConditionConfiguration { Expression = new Expression("true") } };
        var transformation = new TransformationDefinition { Engine = EngineType.DSL, Configuration = new DslTransformationConfiguration { Dsl = "map" } };
        var taskRepository = Substitute.For<ITaskRepository>();
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        var taskHandler = new UpdateTaskDefinitionCommandHandler(taskRepository);

        Assert.True(await taskHandler.Handle(
            new UpdateTaskDefinitionCommand(
                task.Id.ToString(),
                "inventories.reserve",
                "Reserve inventory",
                7,
                "notes",
                TaskKind.Messaging,
                TaskExecutionMode.Parallel,
                groupId.ToString(),
                condition,
                transformation,
                new MessagingTaskConfiguration { Topic = "inventories.reserve" },
                null!,
                null!,
                OnErrorPolicy.StopAndCompensate,
                null!,
                TaskDispatchType.FireAndWaitCallback,
                false),
            CancellationToken.None));

        Assert.Equal("inventories.reserve", task.Key);
        Assert.Equal(7, task.Order);
        Assert.Equal(groupId, task.ParallelGroupId);
        Assert.True(task.HasExecutionCondition);
        Assert.True(task.HasTransformation);
        Assert.False(task.IsEnabled);
        await taskRepository.Received(1).Update(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrchestrationVersionEditGuardAllowsDraftAndRejectsNonDraft()
    {
        var version = Version(OrchestrationVersionStatus.Draft);
        var stage = new StageDefinition { Id = Id.New(), OrchestrationVersionId = version.Id, Key = "stage", Name = "Stage", Order = 1 };
        var task = new TaskDefinition
        {
            Id = Id.New(),
            StageDefinitionId = stage.Id,
            Key = "task",
            Name = "Task",
            Order = 1,
            Kind = TaskKind.Messaging,
            ExecutionMode = TaskExecutionMode.Sequential,
            Configuration = new MessagingTaskConfiguration { Topic = "topic" },
            DispatchType = TaskDispatchType.FireAndWaitCallback,
            IsEnabled = true
        };
        var trigger = new TriggerBinding
        {
            Id = Id.New(),
            OrchestrationVersionId = version.Id,
            Key = "trigger",
            TriggerType = TriggerType.Event,
            TriggerChannel = new EventTriggerChannel { Topic = "events", Version = new SemanticVersion(1, 0, 0) },
            IsEnabled = true
        };
        var variable = new VariableDefinition { Id = Id.New(), OrchestrationVersionId = version.Id, Key = "tenant" };
        var group = new ParallelGroupDefinition { Id = Id.New(), StageDefinitionId = stage.Id, Name = "group", JoinPolicy = ParallelJoinPolicy.WaitAll };
        var branchFromTask = new BranchRuleDefinition
        {
            Id = Id.New(),
            FromType = ElementType.Task,
            FromId = task.Id,
            NavigateToType = ElementType.Stage,
            NavigateToId = stage.Id,
            Condition = Condition()
        };
        var branchFromStage = new BranchRuleDefinition
        {
            Id = Id.New(),
            FromType = ElementType.Stage,
            FromId = stage.Id,
            NavigateToType = ElementType.Task,
            NavigateToId = task.Id,
            Condition = Condition()
        };
        var versionRepository = Substitute.For<IOrchestrationVersionRepository>();
        versionRepository.GetById(version.Id, Arg.Any<CancellationToken>()).Returns(version);
        var stageRepository = Substitute.For<IStageRepository>();
        stageRepository.GetById(stage.Id, Arg.Any<CancellationToken>()).Returns(stage);
        var taskRepository = Substitute.For<ITaskRepository>();
        taskRepository.GetById(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        var triggerRepository = Substitute.For<ITriggerBindingRepository>();
        triggerRepository.GetById(trigger.Id, Arg.Any<CancellationToken>()).Returns(trigger);
        var variableRepository = Substitute.For<IVariableDefinitionRepository>();
        variableRepository.GetById(variable.Id, Arg.Any<CancellationToken>()).Returns(variable);
        var parallelGroupRepository = Substitute.For<IParallelGroupRepository>();
        parallelGroupRepository.GetById(group.Id, Arg.Any<CancellationToken>()).Returns(group);
        var branchRuleRepository = Substitute.For<IBranchRuleRepository>();
        branchRuleRepository.GetById(branchFromTask.Id, Arg.Any<CancellationToken>()).Returns(branchFromTask);
        branchRuleRepository.GetById(branchFromStage.Id, Arg.Any<CancellationToken>()).Returns(branchFromStage);
        var guard = new OrchestrationVersionEditGuard(
            versionRepository,
            stageRepository,
            taskRepository,
            triggerRepository,
            variableRepository,
            parallelGroupRepository,
            branchRuleRepository);

        await guard.EnsureVersionIsDraft(version.Id.ToString());
        await guard.EnsureStageVersionIsDraft(stage.Id.ToString());
        await guard.EnsureTaskVersionIsDraft(task.Id.ToString());
        await guard.EnsureTriggerVersionIsDraft(trigger.Id.ToString());
        await guard.EnsureVariableVersionIsDraft(variable.Id.ToString());
        await guard.EnsureParallelGroupVersionIsDraft(group.Id.ToString());
        await guard.EnsureBranchRuleVersionIsDraft(branchFromTask.Id.ToString());
        await guard.EnsureBranchRuleVersionIsDraft(branchFromStage.Id.ToString());

        version.Status = OrchestrationVersionStatus.Deployed;
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.EnsureVersionIsDraft(version.Id.ToString()));
    }

    private static OrchestrationVersion Version(OrchestrationVersionStatus status)
        => new()
        {
            Id = Id.New(),
            OrchestrationDefinitionId = Id.New(),
            Version = new SemanticVersion(1, 0, 0),
            Status = status,
            VersionLabel = "1.0.0",
            Checksum = new Checksum("checksum"),
            CreatedOnUtc = DateTime.UtcNow,
            CreatedBy = "tests",
        };

    private static ExecutionCondition Condition()
        => new()
        {
            Engine = EngineType.DSL,
            Configuration = new DslConditionConfiguration { Expression = new Expression("true") },
        };

    private sealed class LifecycleFixture
    {
        public LifecycleFixture(OrchestrationVersionStatus status)
        {
            Version = Version(status);
            Definition = new OrchestrationDefinition
            {
                Id = Version.OrchestrationDefinitionId,
                Key = "sales.sale.created",
                Name = "Sale Created",
                Domain = "sales",
                DomainId = Id.New(),
                IsActive = true,
                CreatedOnUtc = DateTime.UtcNow,
                CreatedBy = "tests"
            };
            VersionRepository.GetById(Version.Id, Arg.Any<CancellationToken>()).Returns(Version);
            DefinitionRepository.GetById(Definition.Id, Arg.Any<CancellationToken>()).Returns(Definition);
            SnapshotBuilder.Build(Version, Arg.Any<CancellationToken>()).Returns(Version);
            PayloadFactory.CreatePayloadJson(Definition, Version).Returns("""{"artifact":true}""");
            VersionRepository.Update(Arg.Any<OrchestrationVersion>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }

        public OrchestrationVersion Version { get; }
        public OrchestrationDefinition Definition { get; }
        public IOrchestrationVersionRepository VersionRepository { get; } = Substitute.For<IOrchestrationVersionRepository>();
        public IOrchestrationDefinitionRepository DefinitionRepository { get; } = Substitute.For<IOrchestrationDefinitionRepository>();
        public IOrchestrationVersionTransitionPolicy TransitionPolicy { get; } = Substitute.For<IOrchestrationVersionTransitionPolicy>();
        public IOrchestrationVersionArtifactSnapshotBuilder SnapshotBuilder { get; } = Substitute.For<IOrchestrationVersionArtifactSnapshotBuilder>();
        public IOrchestrationArtifactPayloadFactory PayloadFactory { get; } = Substitute.For<IOrchestrationArtifactPayloadFactory>();
        public IOrchestrationArtifactDslValidationService DslValidationService { get; } = Substitute.For<IOrchestrationArtifactDslValidationService>();
        public IArtifactPublicationApplicationService PublicationService { get; } = Substitute.For<IArtifactPublicationApplicationService>();
    }
}
