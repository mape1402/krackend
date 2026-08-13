using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Intake;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions;
using Krackend.Sagas.Orchestrations.Messaging.Abstractions.Publishing;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

namespace Krackend.Sagas.Orchestrations.Engine;

/// <summary>
/// Registers runtime engine services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the runtime engine and its default messaging facade implementation.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddKrackendSagasOrchestrationsEngine(this IServiceCollection services)
    {
        services.AddKrackendSagasOrchestrationsMessaging();
        services.TryAddSingleton<IMessagePublisher, InMemoryMessagePublisher>();
        services.AddScoped<IMessagingCommandDispatcher, MessagingCommandDispatcher>();
        services.AddScoped<IRuntimeTaskDispatcher, MessagingRuntimeTaskDispatcher>();
        services.AddScoped<IRuntimeTaskDispatcherResolver, RuntimeTaskDispatcherResolver>();
        services.AddScoped<IRuntimeConditionEvaluator, RuntimeConditionEvaluator>();
        services.AddScoped<IRuntimePayloadTransformer, RuntimePayloadTransformer>();
        services.AddScoped<IRuntimeRetryPolicyEvaluator, RuntimeRetryPolicyEvaluator>();
        services.AddScoped<IRuntimeTimeoutPolicyEvaluator, RuntimeTimeoutPolicyEvaluator>();
        services.AddScoped<IRuntimeErrorPolicyResolver, RuntimeErrorPolicyResolver>();
        services.AddScoped<IRuntimeCompensationPlanBuilder, RuntimeCompensationPlanBuilder>();
        services.AddScoped<IRuntimeCompensationExecutor, RuntimeCompensationExecutor>();
        services.AddScoped<IRuntimePendingWorkProcessor, RuntimePendingWorkProcessor>();
        services.AddScoped<IArtifactResolver, ArtifactResolver>();
        services.AddScoped<ITriggerPromoter, TriggerPromoter>();
        services.TryAddSingleton<IRuntimeReactiveEventPublisher, NoopRuntimeReactiveEventPublisher>();
        services.AddScoped(CreateRuntimeEngineDependencies);
        services.AddScoped<IRuntimeEngine, RuntimeEngine>();
        return services;
    }

    private static RuntimeEngineDependencies CreateRuntimeEngineDependencies(IServiceProvider provider)
    {
        return new RuntimeEngineDependencies
        {
            IntakeBuffer = provider.GetRequiredService<ITriggerIntakeBuffer>(),
            TriggerPromoter = provider.GetRequiredService<ITriggerPromoter>(),
            ArtifactRepository = provider.GetRequiredService<IRuntimeArtifactRepository>(),
            StageRepository = provider.GetRequiredService<IStageExecutionRepository>(),
            TaskRepository = provider.GetRequiredService<ITaskExecutionRepository>(),
            AttemptRepository = provider.GetRequiredService<ITaskExecutionAttemptRepository>(),
            DispatchRepository = provider.GetRequiredService<ITaskDispatchRepository>(),
            CompensationRepository = provider.GetRequiredService<ICompensationExecutionRepository>(),
            InstanceRepository = provider.GetRequiredService<IOrchestrationInstanceRepository>(),
            TimelineRepository = provider.GetRequiredService<IExecutionTransitionRepository>(),
            TaskDispatcherResolver = provider.GetRequiredService<IRuntimeTaskDispatcherResolver>(),
            ConditionEvaluator = provider.GetRequiredService<IRuntimeConditionEvaluator>(),
            PayloadTransformer = provider.GetRequiredService<IRuntimePayloadTransformer>(),
            RetryPolicyEvaluator = provider.GetRequiredService<IRuntimeRetryPolicyEvaluator>(),
            TimeoutPolicyEvaluator = provider.GetRequiredService<IRuntimeTimeoutPolicyEvaluator>(),
            ErrorPolicyResolver = provider.GetRequiredService<IRuntimeErrorPolicyResolver>(),
            ReactiveEventPublisher = provider.GetRequiredService<IRuntimeReactiveEventPublisher>()
        };
    }
}
