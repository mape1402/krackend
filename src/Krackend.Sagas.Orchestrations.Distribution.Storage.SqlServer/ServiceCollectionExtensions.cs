using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Distribution.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer.Repositories;
using Sieve.Models;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorDistributionStorageSqlServer(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<DistributionStorageDbContext>(configureDbContext);
        services.Configure<SieveOptions>(options =>
        {
            options.CaseSensitive = false;
            options.ThrowExceptions = true;
        });
        services.AddScoped<ISieveProcessor, SieveProcessor>();
        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<IRuntimeNodeRepository, RuntimeNodeRepository>();
        services.AddScoped<IOrchestrationProjectionRepository, OrchestrationProjectionRepository>();
        services.AddScoped<IOrchestrationNodePolicyRepository, OrchestrationNodePolicyRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IReleaseRepository, ReleaseRepository>();
        services.AddScoped<IReleaseTargetRepository, ReleaseTargetRepository>();
        return services;
    }
}


