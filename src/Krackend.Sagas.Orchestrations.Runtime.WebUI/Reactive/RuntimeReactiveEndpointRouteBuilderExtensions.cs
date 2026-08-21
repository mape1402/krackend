using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

/// <summary>
/// Maps runtime diagnostics SignalR endpoints.
/// </summary>
public static class RuntimeReactiveEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the runtime diagnostics SignalR hub using the configured route prefix.
    /// </summary>
    public static IEndpointRouteBuilder MapOrchestratorRuntimeReactiveHub(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<OrchestratorRuntimeWebUIOptions>>().Value;
        var prefix = string.IsNullOrWhiteSpace(options.RoutePrefix) ? "runtime" : options.RoutePrefix.Trim('/');
        endpoints.MapHub<RuntimeReactiveHub>($"/{prefix}/live");
        return endpoints;
    }
}
