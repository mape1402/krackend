using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap.Eventing;
using Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap.Options;
using Krackend.Sagas.Orchestrations.Design.Interaction;
using Krackend.Sagas.Orchestrations.Design.Storage.SqlServer;
using Krackend.Sagas.Orchestrations.Design.WebUI;
using Krackend.Sagas.Orchestrations.Distribution.Interaction;
using Krackend.Sagas.Orchestrations.Distribution.Storage.SqlServer;
using Krackend.Sagas.Orchestrations.Distribution.WebUI;
using Krackend.Sagas.Orchestrations.Security.Interaction;
using Krackend.Sagas.Orchestrations.Security.Storage.SqlServer;
using Krackend.Sagas.Orchestrations.Security.WebUI;
using Krackend.Sagas.Orchestrations.WebUI.Shell;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Bootstrap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorControlPlane(
        this IServiceCollection services,
        Action<ControlPlaneModuleOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new ControlPlaneModuleOptions();
        configure(options);

        if (options.ConfigureSqlServer is null)
        {
            throw new InvalidOperationException("ConfigureSqlServer must be provided.");
        }

        var adminRootPath = NormalizePrefix(options.AdminRootPath);

        services.AddOrchestratorWebUIShell();
        services.AddScoped<IIntegrationEventPublisher, InProcessIntegrationEventPublisher>();

        services.AddOrchestratorDesignWebUI(ui => ui.RoutePrefix = adminRootPath);
        services.AddOrchestratorDesignInteraction();
        services.AddOrchestratorDesignStorageSqlServer(options.ConfigureSqlServer);

        services.AddOrchestratorDistributionWebUI(ui => ui.RoutePrefix = $"{adminRootPath}/orchestrator-distribution");
        services.AddOrchestratorDistributionInteraction();
        services.AddOrchestratorDistributionStorageSqlServer(options.ConfigureSqlServer);

        services.AddOrchestratorSecurityWebUI(ui => ui.RoutePrefix = $"{adminRootPath}/orchestrator-security");
        services.AddOrchestratorSecurityInteraction();
        services.AddOrchestratorSecurityStorageSqlServer(options.ConfigureSqlServer);

        return services;
    }

    private static string NormalizePrefix(string routePrefix)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            return "admin";
        }

        return routePrefix.Trim().Trim('/');
    }
}
