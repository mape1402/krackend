using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Dashboard;

/// <summary>Displays deferred statistics and live runtime activity.</summary>
public sealed class IndexModel(IServiceProvider services, IOptions<OrchestratorRuntimeWebUIOptions> options) : PageModel
{
    public SagaStatisticsReport Report { get; private set; }
    public int Days { get; private set; }
    public string OrchestrationKey { get; private set; }
    public string LiveHubPath => $"/{(string.IsNullOrWhiteSpace(options.Value.RoutePrefix) ? "runtime" : options.Value.RoutePrefix.Trim('/'))}/live";

    public async Task OnGetAsync(int days = 7, string orchestrationKey = null, CancellationToken cancellationToken = default)
    {
        Days = days is 1 or 7 or 30 or 90 ? days : 7;
        OrchestrationKey = orchestrationKey?.Trim();
        if (services.GetService(typeof(ISagaStatisticsReader)) is not ISagaStatisticsReader reader) return;
        var before = DateTime.UtcNow.Date.AddDays(1);
        Report = await reader.GetReportAsync(before.AddDays(-Days), before, OrchestrationKey, cancellationToken);
    }

    public async Task<IActionResult> OnGetStatsAsync(int days = 7, string orchestrationKey = null, CancellationToken cancellationToken = default)
    {
        await OnGetAsync(days, orchestrationKey, cancellationToken);
        if (Report is null) return StatusCode(503, "The configured runtime storage does not provide statistics.");
        var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializer.Converters.Add(new JsonStringEnumConverter());
        return new JsonResult(Report, serializer);
    }
}
