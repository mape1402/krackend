using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>Registers shared web UI middleware.</summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>Renders host-configured Zoorce status pages for failed browser requests.</summary>
    public static IApplicationBuilder UseOrchestratorWebUIStatusPages(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseWhen(
            context => IsBrowserNavigation(context.Request),
            branch => branch.UseStatusCodePagesWithReExecute("/_orchestrator/status/{0}"));
    }

    private static bool IsBrowserNavigation(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) ||
            request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            request.Path.StartsWithSegments("/_orchestrator/status", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var accept = request.Headers.Accept.ToString();
        return string.IsNullOrWhiteSpace(accept) ||
            accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
