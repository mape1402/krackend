using Microsoft.Extensions.DependencyInjection;
using DesignApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ServiceCollectionExtensions;
using DistributionApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution.ServiceCollectionExtensions;
using SecurityApplicationServices = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ServiceCollectionExtensions;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application;

/// <summary>
/// Registers control-plane application services for design, distribution, and security workflows.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the full orchestration control-plane application layer.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>Configured service collection.</returns>
    public static IServiceCollection AddOrchestratorControlPlaneApplication(this IServiceCollection services)
    {
        DesignApplicationServices.AddOrchestratorDesignApplication(services);
        DistributionApplicationServices.AddOrchestratorDistributionApplication(services);
        SecurityApplicationServices.AddOrchestratorSecurityApplication(services);
        return services;
    }
}
