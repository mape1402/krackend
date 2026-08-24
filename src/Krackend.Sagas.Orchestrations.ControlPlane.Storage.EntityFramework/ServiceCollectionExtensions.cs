using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Security.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Distribution.Repositories;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Security.Repositories;
using Sieve.Services;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;

/// <summary>
/// Registers Entity Framework storage services for the orchestration control plane.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the control-plane Entity Framework storage adapter.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configureDbContext">Callback used to configure the Entity Framework provider.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorControlPlaneStorageEntityFramework(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        if (configureDbContext is null)
        {
            throw new ArgumentNullException(nameof(configureDbContext));
        }

        services.AddDbContext<ControlPlaneDbContext>(configureDbContext);
        services.TryAddScoped<IControlPlaneUnitOfWork, EntityFrameworkControlPlaneUnitOfWork>();
        services.TryAddSingleton<ISieveProcessor, SieveProcessor>();

        services.AddScoped<IDomainRepository, DomainRepository>();
        services.AddScoped<IOrchestrationDefinitionRepository, OrchestrationDefinitionRepository>();
        services.AddScoped<IOrchestrationVersionRepository, OrchestrationVersionRepository>();
        services.AddScoped<IStageRepository, StageRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITriggerBindingRepository, TriggerBindingRepository>();
        services.AddScoped<IVariableDefinitionRepository, VariableDefinitionRepository>();
        services.AddScoped<IParallelGroupRepository, ParallelGroupRepository>();
        services.AddScoped<IBranchRuleRepository, BranchRuleRepository>();

        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<IRuntimeNodeRepository, RuntimeNodeRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IReleaseRepository, ReleaseRepository>();
        services.AddScoped<IReleaseTargetRepository, ReleaseTargetRepository>();
        services.AddScoped<IOrchestrationNodePolicyRepository, OrchestrationNodePolicyRepository>();

        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamMemberRepository, TeamMemberRepository>();

        return services;
    }
}
