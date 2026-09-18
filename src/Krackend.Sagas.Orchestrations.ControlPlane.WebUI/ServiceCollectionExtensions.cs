using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.ControlPlane.Application;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using DesignWebUIServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ServiceCollectionExtensions;
using DistributionWebUIServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.ServiceCollectionExtensions;
using SecurityWebUIServices = Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Security.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI;

/// <summary>
/// Registers the orchestration control-plane Web UI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the full control-plane composition: Web UI, application services, and Entity Framework storage.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configureOptions">Control-plane composition configuration callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorControlPlane(
        this IServiceCollection services,
        Action<OrchestratorControlPlaneOptions> configureOptions)
    {
        if (configureOptions is null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var options = new OrchestratorControlPlaneOptions();
        configureOptions(options);

        if (options.ConfigureStorage is null)
        {
            throw new InvalidOperationException("ConfigureStorage must be provided.");
        }

        var adminRootPath = NormalizePrefix(options.AdminRootPath, "admin");

        services.AddOrchestratorControlPlaneWebUI(ui =>
        {
            ui.DesignRoutePrefix = adminRootPath;
            ui.DefaultSchemaRegistryProviderKey = NormalizeSchemaRegistryProviderKey(options.DefaultSchemaRegistryProviderKey);
            ui.DistributionRoutePrefix = $"{adminRootPath}/orchestrator-distribution";
            ui.SecurityRoutePrefix = $"{adminRootPath}/orchestrator-security";
        });
        services.AddOrchestratorControlPlaneApplication();
        services.AddOrchestratorControlPlaneStorageEntityFramework(options.ConfigureStorage, options.ConfigureStorageModel);

        return services;
    }

    /// <summary>
    /// Adds the full control-plane Web UI with default routes.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorControlPlaneWebUI(this IServiceCollection services)
        => services.AddOrchestratorControlPlaneWebUI(_ => { });

    /// <summary>
    /// Adds the full control-plane Web UI.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="configureOptions">Route configuration callback.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorControlPlaneWebUI(
        this IServiceCollection services,
        Action<OrchestratorControlPlaneWebUIOptions> configureOptions)
    {
        if (configureOptions is null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var options = new OrchestratorControlPlaneWebUIOptions();
        configureOptions(options);

        services.AddOrchestratorWebUIShell();
        DesignWebUIServices.AddOrchestratorDesignWebUI(services, ui =>
        {
            ui.RoutePrefix = NormalizePrefix(options.DesignRoutePrefix, "admin");
            ui.DefaultSchemaRegistryProviderKey = NormalizeSchemaRegistryProviderKey(options.DefaultSchemaRegistryProviderKey);
        });
        DistributionWebUIServices.AddOrchestratorDistributionWebUI(services, ui => ui.RoutePrefix = NormalizePrefix(options.DistributionRoutePrefix, "admin/orchestrator-distribution"));
        SecurityWebUIServices.AddOrchestratorSecurityWebUI(services, ui => ui.RoutePrefix = NormalizePrefix(options.SecurityRoutePrefix, "admin/orchestrator-security"));
        return services;
    }

    private static string NormalizePrefix(string routePrefix, string fallback)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            return fallback;
        }

        var normalized = routePrefix.Trim().Trim('/');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeSchemaRegistryProviderKey(string providerKey)
        => string.IsNullOrWhiteSpace(providerKey) ? "knowl" : providerKey.Trim();
}
