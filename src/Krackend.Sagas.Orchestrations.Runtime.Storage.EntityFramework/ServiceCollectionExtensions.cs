using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Ingress;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorRuntimeStorageEntityFramework(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<RuntimeDbContext>(configureDbContext);
        services.Replace(ServiceDescriptor.Scoped<IRuntimeStorageUnitOfWork, RuntimeStorageUnitOfWork>());
        services.Replace(ServiceDescriptor.Scoped<IRuntimeArtifactRepository, RuntimeArtifactRepository>());
        services.Replace(ServiceDescriptor.Scoped<IOrchestrationInstanceRepository, OrchestrationInstanceRepository>());
        services.Replace(ServiceDescriptor.Scoped<IStageExecutionRepository, StageExecutionRepository>());
        services.Replace(ServiceDescriptor.Scoped<ITaskExecutionRepository, TaskExecutionRepository>());
        services.Replace(ServiceDescriptor.Scoped<ITaskExecutionAttemptRepository, TaskExecutionAttemptRepository>());
        services.Replace(ServiceDescriptor.Scoped<ITaskDispatchRepository, TaskDispatchRepository>());
        services.Replace(ServiceDescriptor.Scoped<IExecutionTransitionRepository, ExecutionTransitionRepository>());
        services.Replace(ServiceDescriptor.Scoped<IInstanceVariableRepository, InstanceVariableRepository>());
        services.Replace(ServiceDescriptor.Scoped<IEnvironmentVariableRepository, EnvironmentVariableRepository>());
        services.Replace(ServiceDescriptor.Scoped<ICompensationExecutionRepository, CompensationExecutionRepository>());
        services.Replace(ServiceDescriptor.Scoped<IRuntimeIngressConfigurationRepository, RuntimeIngressConfigurationRepository>());
        services.Replace(ServiceDescriptor.Scoped<IGetAllIngressConfigurationsAccessor, RuntimeIngressConfigurationAccessor>());
        services.Replace(ServiceDescriptor.Scoped<IGetIngressConfigurationByArtifactAccessor, RuntimeIngressConfigurationAccessor>());
        services.TryAddSingleton<IRuntimeReactiveEventPublisher, NoopRuntimeReactiveEventPublisher>();
        return services;
    }
}
