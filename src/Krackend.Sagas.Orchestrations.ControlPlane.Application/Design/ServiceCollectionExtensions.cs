using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

/// <summary>
/// Defines dependency injection registration extensions for the design application layer.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers design application services, handlers, validators, and pipeline behaviors.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorDesignApplication(this IServiceCollection services)
    {
        services.AddPelican(typeof(ServiceCollectionExtensions).Assembly);
        services.AddValidatorsFromAssemblyContaining<CreateOrchestrationDefinitionCommandValidator>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddTransient<IDomainApplicationMapper, DomainApplicationMapper>();
        services.AddTransient<IOrchestrationDefinitionApplicationMapper, OrchestrationDefinitionApplicationMapper>();
        services.AddTransient<IOrchestrationVersionApplicationMapper, OrchestrationVersionApplicationMapper>();
        services.AddTransient<IStageDefinitionApplicationMapper, StageDefinitionApplicationMapper>();
        services.AddTransient<ITaskDefinitionApplicationMapper, TaskDefinitionApplicationMapper>();
        services.AddTransient<ITriggerBindingApplicationMapper, TriggerBindingApplicationMapper>();
        services.AddTransient<IVariableDefinitionApplicationMapper, VariableDefinitionApplicationMapper>();
        services.AddTransient<IParallelGroupDefinitionApplicationMapper, ParallelGroupDefinitionApplicationMapper>();
        services.AddTransient<IBranchRuleDefinitionApplicationMapper, BranchRuleDefinitionApplicationMapper>();
        services.AddScoped<IDomainApplicationService, DomainApplicationService>();
        services.AddScoped<IOrchestrationApplicationService, OrchestrationApplicationService>();
        services.AddScoped<IOrchestrationVersionApplicationService, OrchestrationVersionApplicationService>();
        services.AddScoped<IStageApplicationService, StageApplicationService>();
        services.AddScoped<ITaskApplicationService, TaskApplicationService>();
        services.AddScoped<ITriggerBindingApplicationService, TriggerBindingApplicationService>();
        services.AddScoped<IVariableApplicationService, VariableApplicationService>();
        services.AddScoped<IParallelGroupApplicationService, ParallelGroupApplicationService>();
        services.AddScoped<IBranchRuleApplicationService, BranchRuleApplicationService>();
        services.AddScoped<IOrchestrationVersionArtifactSnapshotBuilder, OrchestrationVersionArtifactSnapshotBuilder>();
        services.AddSingleton<IOrchestrationVersionTransitionPolicy, OrchestrationVersionTransitionPolicy>();
        return services;
    }
}



