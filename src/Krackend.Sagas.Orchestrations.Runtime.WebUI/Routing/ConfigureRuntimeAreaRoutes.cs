using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Routing;

internal sealed class ConfigureRuntimeAreaRoutes : IConfigureOptions<RazorPagesOptions>
{
    private readonly OrchestratorRuntimeWebUIOptions _options;

    public ConfigureRuntimeAreaRoutes(IOptions<OrchestratorRuntimeWebUIOptions> options)
    {
        _options = options.Value;
    }

    public void Configure(RazorPagesOptions options)
    {
        var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/');
        options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/Instances/Index", prefix);
        options.Conventions.AddAreaPageRoute("OrchestratorRuntime", "/Instances/Index", $"{prefix}/instances");
    }
}
