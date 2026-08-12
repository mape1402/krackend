using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Reactive;

public static class RuntimeReactiveEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOrchestratorRuntimeReactiveHub(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<OrchestratorRuntimeWebUIOptions>>().Value;
        var prefix = string.IsNullOrWhiteSpace(options.RoutePrefix) ? "runtime" : options.RoutePrefix.Trim('/');
        endpoints.MapHub<RuntimeReactiveHub>($"/{prefix}/live");
        return endpoints;
    }
}
