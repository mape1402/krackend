using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;

/// <summary>
/// Provides runtime orchestration diagnostics for the instances dashboard page.
/// </summary>
public sealed class IndexModel : PageModel
{
    private readonly IRuntimeDiagnosticsReader _diagnosticsReader;
    private readonly OrchestratorRuntimeWebUIOptions _options;

    public IndexModel(
        IRuntimeDiagnosticsReader diagnosticsReader,
        IOptions<OrchestratorRuntimeWebUIOptions> options)
    {
        _diagnosticsReader = diagnosticsReader;
        _options = options.Value;
    }

    /// <summary>
    /// Gets the SignalR hub path used by the runtime diagnostics dashboard.
    /// </summary>
    public string LiveHubPath
    {
        get
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/');
            return $"/{prefix}/live";
        }
    }

    /// <summary>
    /// Gets orchestration instances displayed in the dashboard grid.
    /// </summary>
    public IReadOnlyCollection<InstanceRowModel> Instances { get; private set; } = Array.Empty<InstanceRowModel>();

    /// <summary>
    /// Gets minute-bucketed traffic displayed by the dashboard chart.
    /// </summary>
    public IReadOnlyCollection<TrafficPointModel> Traffic { get; private set; } = Array.Empty<TrafficPointModel>();

    /// <summary>
    /// Gets hour-bucketed traffic displayed by the dashboard chart.
    /// </summary>
    public IReadOnlyCollection<TrafficPointModel> HourlyTraffic { get; private set; } = Array.Empty<TrafficPointModel>();

    /// <summary>
    /// Gets runtime summary counters displayed by the dashboard.
    /// </summary>
    public RuntimeSummaryModel Summary { get; private set; } = new(0, 0, 0, 0, 0, 0, DateTime.UtcNow, DateTime.UtcNow);

    /// <summary>
    /// Handles the initial dashboard page request.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _diagnosticsReader.GetSnapshot(cancellationToken);
        Instances = snapshot.Instances;
        Traffic = snapshot.Traffic;
        HourlyTraffic = snapshot.HourlyTraffic;
        Summary = snapshot.Summary;
    }

    /// <summary>
    /// Gets a full dashboard snapshot used by client-side reconciliation.
    /// </summary>
    public async Task<IActionResult> OnGetSnapshotAsync(CancellationToken cancellationToken = default)
        => new JsonResult(await _diagnosticsReader.GetSnapshot(cancellationToken));

    /// <summary>
    /// Gets summary counters and traffic data used by live refreshes.
    /// </summary>
    public async Task<IActionResult> OnGetSummaryAsync(CancellationToken cancellationToken = default)
        => new JsonResult(await _diagnosticsReader.GetSummary(cancellationToken));

    /// <summary>
    /// Gets a full orchestration instance trace.
    /// </summary>
    public async Task<IActionResult> OnGetDetailAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return BadRequest();

        return new JsonResult(await _diagnosticsReader.GetDetail(instanceId, cancellationToken));
    }

    /// <summary>
    /// Maps runtime statuses to control-plane badge classes.
    /// </summary>
    public static string StatusClass(string status)
        => RuntimeDiagnosticsReader.StatusClass(status);
}
