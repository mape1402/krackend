using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Infrastructure;
using Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Repositories;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKrackendSagasOrchestrationsSqlServer(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<RuntimeStorageDbContext>(configureDbContext);
        services.AddScoped<IRuntimeArtifactRepository, RuntimeArtifactRepository>();
        services.AddScoped<ITriggerIntakeRepository, TriggerIntakeRepository>();
        services.AddScoped<ITriggerIntakeAttemptRepository, TriggerIntakeAttemptRepository>();
        services.AddScoped<IOrchestrationInstanceRepository, OrchestrationInstanceRepository>();
        services.AddScoped<IStageExecutionRepository, StageExecutionRepository>();
        services.AddScoped<ITaskExecutionRepository, TaskExecutionRepository>();
        services.AddScoped<ITaskExecutionAttemptRepository, TaskExecutionAttemptRepository>();
        services.AddScoped<ITaskDispatchRepository, TaskDispatchRepository>();
        services.AddScoped<IExecutionTransitionRepository, ExecutionTransitionRepository>();
        services.AddScoped<IInstanceVariableRepository, InstanceVariableRepository>();
        services.AddScoped<IEnvironmentVariableRepository, EnvironmentVariableRepository>();
        return services;
    }
}
