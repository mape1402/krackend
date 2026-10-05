using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Registers shared Krackend orchestration Web UI shell services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the shared Web UI shell with default theme options.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorWebUIShell(this IServiceCollection services)
        => services.AddOrchestratorWebUIShell(_ => { });

    /// <summary>
    /// Adds the shared Web UI shell and configures the visual theme.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configureTheme">Theme configuration callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorWebUIShell(
        this IServiceCollection services,
        Action<OrchestratorWebUIThemeOptions> configureTheme)
    {
        if (configureTheme is null)
        {
            throw new ArgumentNullException(nameof(configureTheme));
        }

        services.AddRazorPages()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        services.TryAddSingleton<OrchestratorNavigationRegistry>();
        services.TryAddSingleton<IOrchestratorThemeCssRenderer, DefaultOrchestratorThemeCssRenderer>();
        services.TryAddScoped<IOrchestratorThemeModeAccessor, DefaultOrchestratorThemeModeAccessor>();
        services.AddHttpContextAccessor();
        services.AddOptions<OrchestratorThemeModeCookieOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureShellAreaRoutes>());
        services.Configure(configureTheme);
        return services;
    }

    private sealed class ConfigureShellAreaRoutes : IConfigureOptions<RazorPagesOptions>
    {
        public void Configure(RazorPagesOptions options)
        {
            options.Conventions.AddAreaPageRoute("OrchestratorShell", "/Theme", "_orchestrator/theme-mode");
        }
    }
}
