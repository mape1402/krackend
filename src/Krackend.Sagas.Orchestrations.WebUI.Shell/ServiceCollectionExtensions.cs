using Microsoft.Extensions.DependencyInjection;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorWebUIShell(this IServiceCollection services)
    {
        services.AddSingleton<OrchestratorNavigationRegistry>();
        return services;
    }
}
