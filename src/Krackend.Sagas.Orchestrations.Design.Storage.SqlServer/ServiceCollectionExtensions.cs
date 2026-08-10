using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.Repositories;
using Sieve.Models;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer;

/// <summary>
/// Represents ServiceCollectionExtensions.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Executes AddOrchestratorDesignStorageSqlServer.
    /// </summary>
    public static IServiceCollection AddOrchestratorDesignStorageSqlServer(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<DesignStorageDbContext>(configureDbContext);
        services.Configure<SieveOptions>(options =>
        {
            options.CaseSensitive = false;
            options.ThrowExceptions = true;
        });
        services.AddScoped<ISieveProcessor, SieveProcessor>();

        services.AddScoped<IDomainRepository, DomainRepository>();
        services.AddScoped<ITeamProjectionRepository, TeamProjectionRepository>();
        services.AddScoped<IOrchestrationDefinitionRepository, OrchestrationDefinitionRepository>();
        services.AddScoped<IOrchestrationVersionRepository, OrchestrationVersionRepository>();
        services.AddScoped<IStageRepository, StageRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITriggerBindingRepository, TriggerBindingRepository>();
        services.AddScoped<IVariableDefinitionRepository, VariableDefinitionRepository>();
        services.AddScoped<IParallelGroupRepository, ParallelGroupRepository>();
        services.AddScoped<IBranchRuleRepository, BranchRuleRepository>();

        return services;
    }
}

