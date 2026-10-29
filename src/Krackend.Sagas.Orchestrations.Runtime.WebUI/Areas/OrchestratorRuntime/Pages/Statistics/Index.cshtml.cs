using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Statistics;

/// <summary>Renders an on-demand, optionally realtime, orchestration statistics workspace.</summary>
public sealed class IndexModel(IOptions<OrchestratorRuntimeWebUIOptions> options) : PageModel
{
    public int Hours { get; private set; } = 24;

    public string LiveHubPath => $"/{(string.IsNullOrWhiteSpace(options.Value.RoutePrefix) ? "runtime" : options.Value.RoutePrefix.Trim('/'))}/live";

    /// <summary>Renders the unfiltered shell; data remains idle until the operator loads a scope.</summary>
    public void OnGet(int? hours = null)
        => Hours = hours is 1 or 6 or 24 or 168 or 720 or 2160 ? hours.Value : 24;
}
