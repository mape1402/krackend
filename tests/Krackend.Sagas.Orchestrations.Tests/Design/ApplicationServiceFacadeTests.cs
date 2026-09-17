using Pelican.Mediator;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ConditionConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using DesignApp = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using SecurityApp = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security;

namespace Krackend.Sagas.Orchestrations.Tests.Design;

public sealed class ApplicationServiceFacadeTests
{
    [Fact]
    public async Task DomainApplicationServiceDelegatesEveryOperationToMediator()
    {
        var domain = new DesignApp.DomainModel { Id = "domain-1", Key = "sales", DisplayName = "Sales" };
        var page = new DesignApp.ApplicationPagedResult<DesignApp.DomainModel>(1, 1, 1, 20, [domain]);
        var mediator = new RecordingMediator()
            .With("domain-id")
            .With(true)
            .With(domain)
            .With(page);
        var service = new DesignApp.DomainApplicationService(mediator);

        var upsert = new DesignApp.UpsertDomainCommand("", "sales", "Sales", "Sales domain");
        var setActive = new DesignApp.SetDomainIsActiveCommand("domain-1", true);
        var getById = new DesignApp.GetDomainByIdQuery("domain-1");
        var getAll = new DesignApp.GetDomainsQuery(new DesignApp.ApplicationPagedSettings { PageNumber = 1, PageSize = 20 }, "sale");

        Assert.Equal("domain-id", await service.Upsert(upsert));
        Assert.True(await service.SetIsActive(setActive));
        Assert.Same(domain, await service.GetById(getById));
        Assert.Same(page, await service.GetAll(getAll));
        Assert.Equal([upsert, setActive, getById, getAll], mediator.Requests);
    }

    [Fact]
    public async Task OrchestrationApplicationServiceDelegatesEveryOperationToMediator()
    {
        var definition = new DesignApp.OrchestrationDefinitionModel { Id = "orch-1", Key = "sales.sale.created" };
        var page = new DesignApp.ApplicationPagedResult<DesignApp.OrchestrationDefinitionModel>(1, 1, 1, 20, [definition]);
        var mediator = new RecordingMediator()
            .With("orch-id")
            .With(true)
            .With(definition)
            .With(page);
        var service = new DesignApp.OrchestrationApplicationService(mediator);

        var create = new DesignApp.CreateOrchestrationDefinitionCommand("sales.sale.created", "Sale Created", "domain-1", "", "team-1", ["sales"], "tester");
        var update = new DesignApp.UpdateOrchestrationDefinitionCommand("orch-1", "Sale Created", "domain-1", "", "team-1", ["sales"], "tester");
        var activate = new DesignApp.ActivateOrchestrationDefinitionCommand("orch-1");
        var deactivate = new DesignApp.DeactivateOrchestrationDefinitionCommand("orch-1");
        var getById = new DesignApp.GetOrchestrationDefinitionByIdQuery("orch-1");
        var getAll = new DesignApp.GetOrchestrationDefinitionsQuery(new DesignApp.ApplicationPagedSettings { PageNumber = 1, PageSize = 20 });

        Assert.Equal("orch-id", await service.Create(create));
        Assert.True(await service.Update(update));
        Assert.True(await service.Activate(activate));
        Assert.True(await service.Deactivate(deactivate));
        Assert.Same(definition, await service.GetById(getById));
        Assert.Same(page, await service.GetAll(getAll));
        Assert.Equal([create, update, activate, deactivate, getById, getAll], mediator.Requests);
    }

    [Fact]
    public async Task TeamApplicationServiceDelegatesEveryOperationToMediator()
    {
        var team = new SecurityApp.TeamModel { Id = "team-1", Key = "sales" };
        var member = new SecurityApp.TeamMemberModel { Id = "member-1", TeamId = "team-1", ExternalUserId = "user-1" };
        var page = new SecurityApp.ApplicationPagedResult<SecurityApp.TeamModel>(1, 1, 1, 20, [team]);
        var members = new[] { member };
        var mediator = new RecordingMediator()
            .With("team-id")
            .With(true)
            .With(page)
            .With<IReadOnlyCollection<SecurityApp.TeamMemberModel>>(members);
        var service = new SecurityApp.TeamApplicationService(mediator);

        var upsert = new SecurityApp.UpsertTeamCommand("", "sales", "Sales", "", "tester");
        var setActive = new SecurityApp.SetTeamIsActiveCommand("team-1", true, "tester");
        var addMember = new SecurityApp.AddTeamMemberCommand("team-1", "user-1", "User One");
        var removeMember = new SecurityApp.RemoveTeamMemberCommand("team-1", "user-1");
        var getAll = new SecurityApp.GetTeamsQuery(new SecurityApp.ApplicationPagedSettings { PageNumber = 1, PageSize = 20 }, "sale");
        var getMembers = new SecurityApp.GetTeamMembersQuery("team-1");

        Assert.Equal("team-id", await service.Upsert(upsert));
        Assert.True(await service.SetIsActive(setActive));
        Assert.True(await service.AddMember(addMember));
        Assert.True(await service.RemoveMember(removeMember));
        Assert.Same(page, await service.GetAll(getAll));
        Assert.Same(members, await service.GetMembers(getMembers));
        Assert.Equal([upsert, setActive, addMember, removeMember, getAll, getMembers], mediator.Requests);
    }

    [Fact]
    public async Task StageApplicationServiceDelegatesEveryOperationToMediator()
    {
        var stage = new DesignApp.StageDefinitionModel { Id = "stage-1", Key = "inventory" };
        var stages = new[] { stage };
        var mediator = new RecordingMediator()
            .With("stage-id")
            .With(true)
            .With(stage)
            .With<IEnumerable<DesignApp.StageDefinitionModel>>(stages);
        var service = new DesignApp.StageApplicationService(mediator);

        var create = new DesignApp.CreateStageDefinitionCommand("version-1", "inventory", "Inventory", "", 1, DslCondition("true"));
        var update = new DesignApp.UpdateStageDefinitionCommand("stage-1", "inventory", "Inventory", "", 1, DslCondition("true"));
        var setCondition = new DesignApp.SetStageExecutionConditionCommand("stage-1", DslCondition("$trigger.ok"));
        var delete = new DesignApp.DeleteStageDefinitionCommand("stage-1");
        var getAll = new DesignApp.GetStageDefinitionsQuery("version-1");
        var getById = new DesignApp.GetStageDefinitionByIdQuery("stage-1");

        Assert.Equal("stage-id", await service.Create(create));
        Assert.True(await service.Update(update));
        Assert.True(await service.SetExecutionCondition(setCondition));
        Assert.True(await service.Delete(delete));
        Assert.Same(stages, await service.GetAll(getAll));
        Assert.Same(stage, await service.GetById(getById));
        Assert.Equal([create, update, setCondition, delete, getAll, getById], mediator.Requests);
    }

    [Fact]
    public async Task TaskApplicationServiceDelegatesEveryOperationToMediator()
    {
        var task = new DesignApp.TaskDefinitionModel { Id = "task-1", Key = "inventories.reserve" };
        var tasks = new[] { task };
        var mediator = new RecordingMediator()
            .With("task-id")
            .With(true)
            .With(task)
            .With<IEnumerable<DesignApp.TaskDefinitionModel>>(tasks);
        var service = new DesignApp.TaskApplicationService(mediator);

        var create = CreateTaskDefinitionCommand();
        var update = UpdateTaskDefinitionCommand();
        var delete = new DesignApp.DeleteTaskDefinitionCommand("task-1");
        var enable = new DesignApp.EnableTaskDefinitionCommand("task-1");
        var disable = new DesignApp.DisableTaskDefinitionCommand("task-1");
        var setCondition = new DesignApp.SetTaskExecutionConditionCommand("task-1", DslCondition("$payload.ok"));
        var setTransformation = new DesignApp.SetTaskTransformationCommand("task-1", DslTransformation("map request"));
        var getAll = new DesignApp.GetTaskDefinitionsQuery("stage-1");
        var getById = new DesignApp.GetTaskDefinitionByIdQuery("task-1");

        Assert.Equal("task-id", await service.Create(create));
        Assert.True(await service.Update(update));
        Assert.True(await service.Delete(delete));
        Assert.True(await service.Enable(enable));
        Assert.True(await service.Disable(disable));
        Assert.True(await service.SetExecutionCondition(setCondition));
        Assert.True(await service.SetTransformation(setTransformation));
        Assert.Same(tasks, await service.GetAll(getAll));
        Assert.Same(task, await service.GetById(getById));
        Assert.Equal([create, update, delete, enable, disable, setCondition, setTransformation, getAll, getById], mediator.Requests);
    }

    [Fact]
    public async Task TriggerVariableParallelGroupAndBranchServicesDelegateToMediator()
    {
        var trigger = new DesignApp.TriggerBindingModel { Id = "trigger-1", Key = "sale-created" };
        var triggers = new[] { trigger };
        var variable = new DesignApp.VariableDefinitionModel { Id = "variable-1", Key = "tenant" };
        var variables = new[] { variable };
        var group = new DesignApp.ParallelGroupDefinitionModel { Id = "group-1", Name = "reserve-group" };
        var groups = new[] { group };
        var branch = new DesignApp.BranchRuleDefinitionModel { Id = "branch-1" };
        var branches = new[] { branch };
        var mediator = new RecordingMediator()
            .With("created-id")
            .With(true)
            .With(trigger)
            .With<IEnumerable<DesignApp.TriggerBindingModel>>(triggers)
            .With(variable)
            .With<IEnumerable<DesignApp.VariableDefinitionModel>>(variables)
            .With(group)
            .With<IEnumerable<DesignApp.ParallelGroupDefinitionModel>>(groups)
            .With(branch)
            .With<IEnumerable<DesignApp.BranchRuleDefinitionModel>>(branches);
        var triggerService = new DesignApp.TriggerBindingApplicationService(mediator);
        var variableService = new DesignApp.VariableApplicationService(mediator);
        var groupService = new DesignApp.ParallelGroupApplicationService(mediator);
        var branchService = new DesignApp.BranchRuleApplicationService(mediator);

        var channel = new EventTriggerChannel { Topic = "events.sales.sale.created", Version = new SemanticVersion(1, 0, 0) };
        var createTrigger = new DesignApp.CreateTriggerBindingCommand("version-1", "sale-created", TriggerType.Event, channel, true, "");
        var updateTrigger = new DesignApp.UpdateTriggerBindingCommand("trigger-1", "sale-created", TriggerType.Event, channel, true, "");
        var deleteTrigger = new DesignApp.DeleteTriggerBindingCommand("trigger-1");
        var enableTrigger = new DesignApp.EnableTriggerBindingCommand("trigger-1");
        var disableTrigger = new DesignApp.DisableTriggerBindingCommand("trigger-1");
        var getTriggers = new DesignApp.GetTriggerBindingsQuery("version-1");
        var getTrigger = new DesignApp.GetTriggerBindingByIdQuery("trigger-1");
        var createVariable = new DesignApp.CreateVariableDefinitionCommand("version-1", "tenant", "Tenant", "", VariableScope.Instance, VariableValueType.String, "", true, false);
        var updateVariable = new DesignApp.UpdateVariableDefinitionCommand("variable-1", "tenant", "Tenant", "", VariableScope.Instance, VariableValueType.String, "", true, false);
        var deleteVariable = new DesignApp.DeleteVariableDefinitionCommand("variable-1");
        var getVariables = new DesignApp.GetVariableDefinitionsQuery("version-1");
        var getVariable = new DesignApp.GetVariableDefinitionByIdQuery("variable-1");
        var createGroup = new DesignApp.CreateParallelGroupDefinitionCommand("group-1", "stage-1", "reserve-group", ParallelJoinPolicy.WaitAll, 2);
        var updateGroup = new DesignApp.UpdateParallelGroupDefinitionCommand("group-1", "reserve-group", ParallelJoinPolicy.WaitAll, null);
        var deleteGroup = new DesignApp.DeleteParallelGroupDefinitionCommand("group-1");
        var getGroups = new DesignApp.GetParallelGroupDefinitionsQuery("stage-1");
        var getGroup = new DesignApp.GetParallelGroupDefinitionByIdQuery("group-1");
        var createBranch = new DesignApp.CreateBranchRuleDefinitionCommand(ElementType.Stage, "stage-1", DslCondition("true"), ElementType.Stage, "stage-2");
        var updateBranch = new DesignApp.UpdateBranchRuleDefinitionCommand("branch-1", ElementType.Stage, "stage-1", DslCondition("true"), ElementType.Stage, "stage-2");
        var deleteBranch = new DesignApp.DeleteBranchRuleDefinitionCommand("branch-1");
        var getBranches = new DesignApp.GetBranchRuleDefinitionsQuery("stage-1");
        var getBranch = new DesignApp.GetBranchRuleDefinitionByIdQuery("branch-1");

        Assert.Equal("created-id", await triggerService.Create(createTrigger));
        Assert.True(await triggerService.Update(updateTrigger));
        Assert.True(await triggerService.Delete(deleteTrigger));
        Assert.True(await triggerService.Enable(enableTrigger));
        Assert.True(await triggerService.Disable(disableTrigger));
        Assert.Same(triggers, await triggerService.GetAll(getTriggers));
        Assert.Same(trigger, await triggerService.GetById(getTrigger));
        Assert.Equal("created-id", await variableService.Create(createVariable));
        Assert.True(await variableService.Update(updateVariable));
        Assert.True(await variableService.Delete(deleteVariable));
        Assert.Same(variables, await variableService.GetAll(getVariables));
        Assert.Same(variable, await variableService.GetById(getVariable));
        Assert.Equal("created-id", await groupService.Create(createGroup));
        Assert.True(await groupService.Update(updateGroup));
        Assert.True(await groupService.Delete(deleteGroup));
        Assert.Same(groups, await groupService.GetAll(getGroups));
        Assert.Same(group, await groupService.GetById(getGroup));
        Assert.Equal("created-id", await branchService.Create(createBranch));
        Assert.True(await branchService.Update(updateBranch));
        Assert.True(await branchService.Delete(deleteBranch));
        Assert.Same(branches, await branchService.GetAll(getBranches));
        Assert.Same(branch, await branchService.GetById(getBranch));
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

    private static MessagingTaskConfiguration MessagingConfiguration()
        => new()
        {
            Topic = "inventories.reserve",
            Version = new SemanticVersion(1, 0, 0)
        };

    private static DesignApp.CreateTaskDefinitionCommand CreateTaskDefinitionCommand()
        => new(
            "stage-1",
            "inventories.reserve",
            "Reserve inventory",
            1,
            "",
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            "",
            DslCondition("true"),
            DslTransformation("map request"),
            MessagingConfiguration(),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private static DesignApp.UpdateTaskDefinitionCommand UpdateTaskDefinitionCommand()
        => new(
            "task-1",
            "inventories.reserve",
            "Reserve inventory",
            1,
            "",
            TaskKind.Messaging,
            TaskExecutionMode.Sequential,
            "",
            DslCondition("true"),
            DslTransformation("map request"),
            MessagingConfiguration(),
            null,
            null,
            OnErrorPolicy.Stop,
            null,
            TaskDispatchType.FireAndWaitCallback,
            true);

    private sealed class RecordingMediator : IMediator
    {
        private readonly Dictionary<Type, object> _responses = new();

        public List<object> Requests { get; } = new();

        public RecordingMediator With<T>(T response)
        {
            _responses[typeof(T)] = response!;
            return this;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Requests.Add(request!);
            return Task.CompletedTask;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult((TResponse)_responses[typeof(TResponse)]);
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }
}
