using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Design.WebUI.Navigation;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Design.WebUI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorDesignWebUI(this IServiceCollection services)
    {
        return services.AddOrchestratorDesignWebUI(_ => { });
    }

    public static IServiceCollection AddOrchestratorDesignWebUI(
        this IServiceCollection services,
        Action<OrchestratorDesignWebUIOptions> configureOptions)
    {
        if (configureOptions is null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        services.Configure(configureOptions);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureDesignAreaRoutes>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrchestratorNavigationContributor, DesignNavigationContributor>());
        return services;
    }

    private sealed class ConfigureDesignAreaRoutes : IConfigureOptions<RazorPagesOptions>
    {
        private readonly OrchestratorDesignWebUIOptions _options;

        public ConfigureDesignAreaRoutes(IOptions<OrchestratorDesignWebUIOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public void Configure(RazorPagesOptions options)
        {
            var prefix = NormalizePrefix(_options.RoutePrefix);

            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Index", prefix);
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Create", $"{prefix}/orchestrations/create");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Details", $"{prefix}/orchestrations/{{orchestrationId}}");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationVersions/Details", $"{prefix}/orchestrations/{{orchestrationId}}/versions/{{versionId}}");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationStages/Details", $"{prefix}/orchestrations/{{orchestrationId}}/versions/{{versionId}}/stages/{{stageId}}");
        }

        private static string NormalizePrefix(string routePrefix)
        {
            if (string.IsNullOrWhiteSpace(routePrefix))
            {
                return "orchestrator-design";
            }

            var normalized = routePrefix.Trim();
            normalized = normalized.Trim('/');

            return string.IsNullOrWhiteSpace(normalized)
                ? "orchestrator-design"
                : normalized;
        }
    }
}
