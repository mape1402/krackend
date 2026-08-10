using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Defines dependency injection registration extensions for the interaction layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers interaction services, handlers, validators, and pipeline behaviors.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorDesignInteraction(this IServiceCollection services)
    {
        services.AddPelican(typeof(ServiceCollectionExtensions).Assembly);
        services.AddValidatorsFromAssemblyContaining<CreateOrchestrationDefinitionCommandValidator>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddTransient<IDomainInteractionMapper, DomainInteractionMapper>();
        services.AddTransient<IOrchestrationDefinitionInteractionMapper, OrchestrationDefinitionInteractionMapper>();
        services.AddTransient<IOrchestrationVersionInteractionMapper, OrchestrationVersionInteractionMapper>();
        services.AddTransient<IStageDefinitionInteractionMapper, StageDefinitionInteractionMapper>();
        services.AddTransient<ITaskDefinitionInteractionMapper, TaskDefinitionInteractionMapper>();
        services.AddTransient<ITriggerBindingInteractionMapper, TriggerBindingInteractionMapper>();
        services.AddTransient<IVariableDefinitionInteractionMapper, VariableDefinitionInteractionMapper>();
        services.AddTransient<IParallelGroupDefinitionInteractionMapper, ParallelGroupDefinitionInteractionMapper>();
        services.AddTransient<IBranchRuleDefinitionInteractionMapper, BranchRuleDefinitionInteractionMapper>();
        services.AddScoped<IDomainInteractionService, DomainInteractionService>();
        services.AddScoped<IOrchestrationInteractionService, OrchestrationInteractionService>();
        services.AddScoped<IOrchestrationVersionInteractionService, OrchestrationVersionInteractionService>();
        services.AddScoped<IStageInteractionService, StageInteractionService>();
        services.AddScoped<ITaskInteractionService, TaskInteractionService>();
        services.AddScoped<ITriggerBindingInteractionService, TriggerBindingInteractionService>();
        services.AddScoped<IVariableInteractionService, VariableInteractionService>();
        services.AddScoped<IParallelGroupInteractionService, ParallelGroupInteractionService>();
        services.AddScoped<IBranchRuleInteractionService, BranchRuleInteractionService>();
        services.AddScoped<IOrchestrationVersionArtifactSnapshotBuilder, OrchestrationVersionArtifactSnapshotBuilder>();
        services.AddScoped<ITeamProjectionInteractionService, TeamProjectionInteractionService>();
        services.AddScoped<IIntegrationEventHandler<TeamCreatedEvent>, TeamCreatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<TeamUpdatedEvent>, TeamUpdatedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<TeamDisabledEvent>, TeamDisabledEventHandler>();
        services.AddSingleton<IOrchestrationVersionTransitionPolicy, OrchestrationVersionTransitionPolicy>();
        return services;
    }
}



