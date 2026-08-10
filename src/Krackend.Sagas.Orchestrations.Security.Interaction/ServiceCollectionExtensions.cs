using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Security.Interaction;

/// <summary>
/// Registers security interaction services and handlers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds security interaction services.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorSecurityInteraction(this IServiceCollection services)
    {
        services.AddPelican(typeof(ServiceCollectionExtensions).Assembly);
        services.AddValidatorsFromAssemblyContaining<UpsertTeamCommandValidator>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddScoped<ITeamInteractionService, TeamInteractionService>();
        return services;
    }
}
