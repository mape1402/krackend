using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Navigation;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrchestratorRuntimeWebUI(this IServiceCollection services)
        => services.AddOrchestratorRuntimeWebUI(_ => { });

    public static IServiceCollection AddOrchestratorRuntimeWebUI(
        this IServiceCollection services,
        Action<OrchestratorRuntimeWebUIOptions> configureOptions)
    {
        services.AddOrchestratorWebUIShell();
        services.AddSignalR();
        services.Configure(configureOptions);
        services.TryAddScoped<IRuntimeDiagnosticsReader, RuntimeDiagnosticsReader>();
        services.Replace(ServiceDescriptor.Singleton<IRuntimeReactiveEventPublisher, SignalRRuntimeReactiveEventPublisher>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureRuntimeAreaRoutes>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrchestratorNavigationContributor, RuntimeNavigationContributor>());
        return services;
    }

    private sealed class ConfigureRuntimeAreaRoutes : IConfigureOptions<RazorPagesOptions>
    {
        private readonly OrchestratorRuntimeWebUIOptions _options;

        public ConfigureRuntimeAreaRoutes(IOptions<OrchestratorRuntimeWebUIOptions> options)
        {
            _options = options.Value;
        }

        public void Configure(RazorPagesOptions options)
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/');
            options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/Instances/Index", $"{prefix}/instances");
            options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/Artifacts/Index", prefix);
            options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/Artifacts/Index", $"{prefix}/artifacts");
        }
    }
}
