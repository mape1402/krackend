using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class ControlPlaneStorageRegistrationTests
{
    [Fact]
    public void ControlPlaneEntityFrameworkStorageRegistersUnifiedDbContextRepositoriesAndSieve()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorControlPlaneStorageEntityFramework(_ => { });

        AssertRegistered<ControlPlaneDbContext>(services);
        AssertRegistered<IControlPlaneUnitOfWork>(services);
        AssertRegistered<ISieveProcessor>(services);
        AssertRegistered<IDomainRepository>(services);
        AssertRegistered<IOrchestrationDefinitionRepository>(services);
        AssertRegistered<IOrchestrationVersionRepository>(services);
        AssertRegistered<IStageRepository>(services);
        AssertRegistered<ITaskRepository>(services);
        AssertRegistered<ITriggerBindingRepository>(services);
        AssertRegistered<IVariableDefinitionRepository>(services);
        AssertRegistered<IParallelGroupRepository>(services);
        AssertRegistered<IBranchRuleRepository>(services);
        AssertRegistered<IRuntimeNodeRepository>(services);
        AssertRegistered<IOrchestrationNodePolicyRepository>(services);
        AssertRegistered<IArtifactRepository>(services);
        AssertRegistered<IReleaseRepository>(services);
        AssertRegistered<IReleaseTargetRepository>(services);
        AssertRegistered<ITeamRepository>(services);
        AssertRegistered<ITeamMemberRepository>(services);
    }

    private static void AssertRegistered<TService>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
    }
}
