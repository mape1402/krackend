namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection;

using global::ButterMorph.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Conditions;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Transformations;
using Krackend.Sagas.Orchestrations.Runtime.Engine.Validation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers ButterMorph runtime adapters for Krackend orchestrations.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ButterMorph transform and validation execution services.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddKrackendOrchestrationsRuntimeButterMorph(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddButterMorph();
        services.AddSingleton<IButterMorphDiagnosticMetadataMapper, ButterMorphDiagnosticMetadataMapper>();
        services.AddSingleton<IButterMorphAliasNameFormatter, ButterMorphAliasNameFormatter>();
        services.AddSingleton<IButterMorphSourceGraphBuilder, ButterMorphSourceGraphBuilder>();
        services.AddScoped<IOrchestrationConditionEvaluator, ButterMorphOrchestrationConditionEvaluator>();
        services.AddScoped<IOrchestrationTransformationExecutor, ButterMorphOrchestrationTransformationExecutor>();
        services.AddScoped<IOrchestrationValidationExecutor, ButterMorphOrchestrationValidationExecutor>();

        return services;
    }
}
