using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Mule;
using Mule.EntityFrameworkCore;

namespace Krackend.Sagas.Orchestrations.Tests.EntityFrameworkCore;

public sealed class SqlServerRegistrationTests
{
    [Fact]
    public void RuntimeEntityFrameworkStorageRegistersRuntimeRepositories()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorRuntimeStorageEntityFramework(_ => { });

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RuntimeDbContext));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeArtifactRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrchestrationInstanceRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IStageExecutionRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITaskExecutionRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITaskExecutionAttemptRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITaskDispatchRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICompensationExecutionRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IExecutionTransitionRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRuntimeIngressConfigurationRepository));
    }

    [Fact]
    public void RuntimeArtifactRepositoryQueriesArtifactsFromUnifiedRuntimeStorage()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework",
            "Repositories",
            "RuntimeArtifactRepository.cs"));

        Assert.Contains("GetActive", source);
        Assert.Contains("GetByVersion", source);
        Assert.Contains("DbContext.RuntimeOrchestrationArtifacts", source);
        Assert.DoesNotContain("EnvironmentKey", source);
        Assert.Contains("x.OrchestrationDefinitionKey == orchestrationDefinitionKey", source);
    }

    [Fact]
    public void RuntimeStorageCanHostMuleDurableActions()
    {
        var services = new ServiceCollection();

        services.AddOrchestratorRuntimeStorageEntityFramework(options =>
            options.UseSqlServer("Server=(local);Database=KrackendTests;Trusted_Connection=True;TrustServerCertificate=True"));
        services.AddMule(mule => mule.UseEntityFrameworkCore<RuntimeDbContext>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RuntimeDbContext>();

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
