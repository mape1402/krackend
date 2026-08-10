using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Distribution.WebUI.Navigation;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Distribution.WebUI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorDistributionWebUI(this IServiceCollection services)
        => services.AddOrchestratorDistributionWebUI(_ => { });

    public static IServiceCollection AddOrchestratorDistributionWebUI(this IServiceCollection services, Action<OrchestratorDistributionWebUIOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureDistributionAreaRoutes>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrchestratorNavigationContributor, DistributionNavigationContributor>());
        return services;
    }

    private sealed class ConfigureDistributionAreaRoutes : IConfigureOptions<RazorPagesOptions>
    {
        private readonly OrchestratorDistributionWebUIOptions _options;

        public ConfigureDistributionAreaRoutes(IOptions<OrchestratorDistributionWebUIOptions> options)
        {
            _options = options.Value;
        }

        public void Configure(RazorPagesOptions options)
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "orchestrator-distribution" : _options.RoutePrefix.Trim('/');
            options.Conventions.AddAreaPageRoute("OrchestratorDistribution", "/RuntimeNodes/Index", prefix);
            options.Conventions.AddAreaPageRoute("OrchestratorDistribution", "/ArtifactReleases/Index", $"{prefix}/artifacts");
            options.Conventions.AddAreaPageRoute("OrchestratorDistribution", "/Promotions/Index", $"{prefix}/releases");
        }
    }
}

