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

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeArtifactCatalog));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeArtifactRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITriggerIntakeRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrchestrationInstanceRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITaskDispatchRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICompensationExecutionRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IExecutionTransitionRepository));
    }

    [Fact]
    public void RuntimeArtifactCatalogUsesPagedActiveDeploymentQuery()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer",
            "Repositories",
            "EfCoreRuntimeArtifactCatalog.cs"));

        Assert.Contains("ReadActiveDeployments", source);
        Assert.Contains(".Where(x => x.IsActive && x.ArtifactType == DeployArtifactType)", source);
        Assert.Contains(".Skip(offset)", source);
        Assert.Contains(".Take(pageSize + 1)", source);
        Assert.DoesNotContain("EnvironmentKey", source);
        Assert.DoesNotContain("GetAll", source);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Krackend.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
