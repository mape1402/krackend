using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Distribution.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Security.Storage;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Sieve.Services;
using DesignStorageServices = Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.ServiceCollectionExtensions;
using DistributionStorageServices = Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.ServiceCollectionExtensions;
using SecurityStorageServices = Krackend.Sagas.Orchestrations.Security.Storage.SqlServer.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.Tests.Storage;

public sealed class ControlPlaneStorageRegistrationTests
{
    [Fact]
    public void DesignSqlServerStorageRegistersDbContextRepositoriesAndSieve()
    {
        var services = new ServiceCollection();

        DesignStorageServices.AddOrchestratorDesignStorageSqlServer(services, _ => { });

        AssertRegistered<DesignStorageDbContext>(services);
        AssertRegistered<ISieveProcessor>(services);
        AssertRegistered<IDomainRepository>(services);
        AssertRegistered<ITeamProjectionRepository>(services);
        AssertRegistered<IOrchestrationDefinitionRepository>(services);
        AssertRegistered<IOrchestrationVersionRepository>(services);
        AssertRegistered<IStageRepository>(services);
        AssertRegistered<ITaskRepository>(services);
        AssertRegistered<ITriggerBindingRepository>(services);
        AssertRegistered<IVariableDefinitionRepository>(services);
        AssertRegistered<IParallelGroupRepository>(services);
        AssertRegistered<IBranchRuleRepository>(services);
    }

    [Fact]
    public void DistributionSqlServerStorageRegistersDbContextRepositoriesAndSieve()
    {
        var services = new ServiceCollection();

        DistributionStorageServices.AddOrchestratorDistributionStorageSqlServer(services, _ => { });

        AssertRegistered<DistributionStorageDbContext>(services);
        AssertRegistered<ISieveProcessor>(services);
        AssertRegistered<IEnvironmentRepository>(services);
        AssertRegistered<IRuntimeNodeRepository>(services);
        AssertRegistered<IOrchestrationProjectionRepository>(services);
        AssertRegistered<IOrchestrationNodePolicyRepository>(services);
        AssertRegistered<IArtifactRepository>(services);
        AssertRegistered<IReleaseRepository>(services);
        AssertRegistered<IReleaseTargetRepository>(services);
    }

    [Fact]
    public void SecuritySqlServerStorageRegistersDbContextAndRepositories()
    {
        var services = new ServiceCollection();

        SecurityStorageServices.AddOrchestratorSecurityStorageSqlServer(services, _ => { });

        AssertRegistered<SecurityStorageDbContext>(services);
        AssertRegistered<ITeamRepository>(services);
        AssertRegistered<ITeamMemberRepository>(services);
    }

    private static void AssertRegistered<TService>(IServiceCollection services)
    {
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
    }
}
