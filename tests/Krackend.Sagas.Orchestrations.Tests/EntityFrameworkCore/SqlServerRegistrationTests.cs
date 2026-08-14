using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Mule;

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

    [Fact]
    public void RuntimeStorageCanHostMuleDurableActions()
    {
        var services = new ServiceCollection();

        services.AddKrackendSagasOrchestrationsSqlServer(options =>
            options.UseSqlServer("Server=(local);Database=KrackendTests;Trusted_Connection=True;TrustServerCertificate=True"));
        services.AddMule(mule => mule.UseKrackendSagasOrchestrationsRuntimeStorage());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeStorageDbContext>();

        Assert.NotNull(dbContext.Model.FindEntityType(typeof(DurableAction)));
    }
}
