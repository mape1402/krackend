using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Navigation;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Routing;
using Krackend.Sagas.Orchestrations.WebUI.Shell;
using Krackend.Sagas.Orchestrations.WebUI.Shell.Navigation;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI;

/// <summary>
/// Registers the runtime diagnostics Razor Pages UI module.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the runtime diagnostics UI with default options.
    /// </summary>
    public static IServiceCollection AddOrchestratorRuntimeWebUI(this IServiceCollection services)
        => services.AddOrchestratorRuntimeWebUI(_ => { });

    /// <summary>
    /// Adds the runtime diagnostics UI with configured options.
    /// </summary>
    public static IServiceCollection AddOrchestratorRuntimeWebUI(
        this IServiceCollection services,
        Action<OrchestratorRuntimeWebUIOptions> configureOptions)
    {
        if (configureOptions is null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        services.AddOrchestratorWebUIShell();
        services.AddSignalR();
        services.Configure(configureOptions);
        services.TryAddSingleton<SignalRRuntimeReactiveEventQueue>();
        services.AddHostedService<SignalRRuntimeReactiveEventDispatcher>();
        services.TryAddScoped<IRuntimeDiagnosticsReader, RuntimeDiagnosticsReader>();
        services.Replace(ServiceDescriptor.Singleton<IRuntimeReactiveEventPublisher, SignalRRuntimeReactiveEventPublisher>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RazorPagesOptions>, ConfigureRuntimeAreaRoutes>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOrchestratorNavigationContributor, RuntimeNavigationContributor>());
        return services;
    }
}
