using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Security.WebUI.Navigation;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Security.WebUI;

/// <summary>
/// Registers Security WebUI routes and navigation.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Security WebUI module.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorSecurityWebUI(this IServiceCollection services)
        => services.AddOrchestratorSecurityWebUI(_ => { });

    /// <summary>
    /// Adds Security WebUI module with options.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configureOptions">Options callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorSecurityWebUI(
        this IServiceCollection services,
        Action<OrchestratorSecurityWebUIOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureSecurityAreaRoutes>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrchestratorNavigationContributor, SecurityNavigationContributor>());
        return services;
    }

    private sealed class ConfigureSecurityAreaRoutes : IConfigureOptions<RazorPagesOptions>
    {
        private readonly OrchestratorSecurityWebUIOptions _options;

        public ConfigureSecurityAreaRoutes(IOptions<OrchestratorSecurityWebUIOptions> options)
        {
            _options = options.Value;
        }

        public void Configure(RazorPagesOptions options)
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix)
                ? "orchestrator-security"
                : _options.RoutePrefix.Trim('/');
            options.Conventions.AddAreaPageRoute("OrchestratorSecurity", "/Teams/Index", prefix);
        }
    }
}
