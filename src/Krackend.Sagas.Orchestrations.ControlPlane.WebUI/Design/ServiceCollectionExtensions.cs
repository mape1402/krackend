using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using global::ButterMorph.DependencyInjection;
using global::ButterMorph.Design;
using global::ButterMorph.Json.Schema;
using global::ButterMorph.SchemaDesign;
using global::ButterMorph.Web.Razor;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;
using Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.Navigation;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design;

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

        services.AddRazorPages()
            .AddApplicationPart(typeof(ServiceCollectionExtensions).Assembly);
        services.AddOrchestratorWebUIShell();
        services.Configure(configureOptions);
        services.AddButterMorph();
        services.AddButterMorphJsonSchema();
        services.AddButterMorphSchemaDesign();
        services.AddButterMorphDesign();
        services.AddButterMorphRazorDesigner(options => options.ShowSchemaActions = false);
        services.TryAddSingleton<IOrchestrationButterMorphThemeMapper, OrchestrationButterMorphThemeMapper>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<ButterMorphRazorDesignerOptions>, ConfigureButterMorphDesignerThemeOptions>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureDesignAreaRoutes>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrchestratorNavigationContributor, DesignNavigationContributor>());
        services.TryAddSingleton<IOrchestrationButterMorphDesignerContextParser, OrchestrationButterMorphDesignerContextParser>();
        services.TryAddScoped<IOrchestrationButterMorphSchemaImporter, OrchestrationButterMorphSchemaImporter>();
        services.TryAddScoped<IOrchestrationButterMorphSourceMetadataFactory, OrchestrationButterMorphSourceMetadataFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IButterMorphPayloadSchemaDesignerHost, MetadataDescriptorButterMorphSchemaDesignerHost>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IButterMorphDesignerHost, OrchestrationButterMorphDesignerHost>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IButterMorphValidationDesignerHost, OrchestrationButterMorphValidationDesignerHost>());
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
            var rootPrefix = GetRootPrefix(prefix);

            if (!string.IsNullOrWhiteSpace(rootPrefix))
            {
                options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Index", rootPrefix);
            }

            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Index", prefix);
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Index", $"{prefix}/orchestrations");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Create", $"{prefix}/orchestrations/create");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Orchestrations/Details", $"{prefix}/orchestrations/{{orchestrationId}}");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationVersions/Details", $"{prefix}/orchestrations/{{orchestrationId}}/versions/{{versionId}}");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/OrchestrationStages/Details", $"{prefix}/orchestrations/{{orchestrationId}}/versions/{{versionId}}/stages/{{stageId}}");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Domains/Index", $"{prefix}/domains");
            options.Conventions.AddAreaPageRoute("OrchestratorDesign", "/Metadata/Index", $"{prefix}/metadata");
        }

        private static string NormalizePrefix(string routePrefix)
        {
            if (string.IsNullOrWhiteSpace(routePrefix))
            {
                return "admin/design";
            }

            var normalized = routePrefix.Trim();
            normalized = normalized.Trim('/');

            return string.IsNullOrWhiteSpace(normalized)
                ? "admin/design"
                : normalized;
        }

        private static string GetRootPrefix(string routePrefix)
        {
            var segments = routePrefix.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !string.Equals(segments[^1], "design", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.Join('/', segments.Take(segments.Length - 1));
        }
    }
}
