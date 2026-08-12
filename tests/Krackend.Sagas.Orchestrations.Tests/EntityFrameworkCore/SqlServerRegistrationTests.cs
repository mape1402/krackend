using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace Krackend.Sagas.Orchestrations.Tests.EntityFrameworkCore;

public sealed class SqlServerRegistrationTests
{
    [Fact]
    public void SqlServerAdapterRegistersRuntimeRepositories()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsSqlServer(_ => { });

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeArtifactRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITriggerIntakeRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrchestrationInstanceRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITaskDispatchRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICompensationExecutionRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IExecutionTransitionRepository));
    }
}
