using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
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
    public Task OnGetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Loads a filtered page of instances for the runtime catalog.</summary>
    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string search = null, string sort = null, bool descending = false, string status = null,
        CancellationToken cancellationToken = default, DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedBefore = null, bool activeOnly = false, int? recentHours = null)
    {
        if (_diagnosticsReader is not IRuntimeInstanceCatalogReader catalog)
            return StatusCode(503, "The configured runtime storage does not support paged diagnostics.");
        var error = ValidateFilters(status, startedFrom, startedBefore, recentHours, out var selectedStatus);
        if (error is not null) return BadRequest(error);
        if (recentHours.HasValue) startedFrom = DateTimeOffset.UtcNow.AddHours(-recentHours.Value);
        var sortField = sort switch
        {
            "orchestrationLabel" => RuntimeInstanceCatalogSort.Orchestration,
            "correlationId" => RuntimeInstanceCatalogSort.CorrelationId,
            "sagaId" => RuntimeInstanceCatalogSort.SagaId,
            "status" => RuntimeInstanceCatalogSort.Status,
            "startedOnUtc" => RuntimeInstanceCatalogSort.Started,
            _ => RuntimeInstanceCatalogSort.Started
        };
        RuntimeInstancePageModel result;
        try
        {
            result = await catalog.GetInstances(new RuntimeInstanceCatalogQuery(Math.Max(page, 1), Math.Clamp(pageSize, 1, 100),
                search?.Trim() ?? string.Empty, selectedStatus, sortField, string.IsNullOrEmpty(sort) || descending,
                startedFrom?.UtcDateTime, startedBefore?.UtcDateTime, activeOnly), cancellationToken);
        }
        catch (NotSupportedException)
        {
            return StatusCode(503, "The configured runtime storage does not support paged diagnostics.");
        }
        return new JsonResult(new
        {
            items = result.Items, totalCount = result.TotalCount,
            hasMore = (long)result.PageNumber * result.PageSize < result.TotalCount,
            search = search?.Trim() ?? string.Empty, sort = sort ?? string.Empty, descending, status = status ?? string.Empty
        });
    }

    /// <summary>Counts instances by status using the same filters as the catalog.</summary>
    public async Task<IActionResult> OnGetCountsAsync(string search = null, string status = null,
        DateTimeOffset? startedFrom = null, DateTimeOffset? startedBefore = null, bool activeOnly = false, int? recentHours = null,
        CancellationToken cancellationToken = default)
    {
        if (_diagnosticsReader is not IRuntimeInstanceStatusCountsReader countsReader)
            return StatusCode(503, "The configured runtime storage does not support status counts.");
        var error = ValidateFilters(status, startedFrom, startedBefore, recentHours, out var selectedStatus);
        if (error is not null) return BadRequest(error);
        if (recentHours.HasValue) startedFrom = DateTimeOffset.UtcNow.AddHours(-recentHours.Value);

        var query = new RuntimeInstanceCatalogQuery(1, 1, search?.Trim() ?? string.Empty, selectedStatus,
            RuntimeInstanceCatalogSort.Started, true, startedFrom?.UtcDateTime, startedBefore?.UtcDateTime, activeOnly);
        try
        {
            return new JsonResult(await countsReader.GetStatusCounts(query, cancellationToken));
        }
        catch (NotSupportedException)
        {
            return StatusCode(503, "The configured runtime storage does not support status counts.");
        }
    }

    private static string ValidateFilters(string status, DateTimeOffset? startedFrom, DateTimeOffset? startedBefore, int? recentHours,
        out OrchestrationInstanceStatus? selectedStatus)
    {
        selectedStatus = null;
        if (recentHours.HasValue && recentHours is not (1 or 6 or 24 or 168 or 720 or 2160))
            return "The recent time range must be 1, 6, 24, 168, 720, or 2160 hours and cannot be combined with explicit dates.";
        if (recentHours.HasValue && (startedFrom.HasValue || startedBefore.HasValue))
            return "The recent time range cannot be combined with explicit dates.";
        if (startedFrom.HasValue && startedBefore.HasValue && startedFrom.Value >= startedBefore.Value)
            return "The start of the time range must be earlier than its end.";
        if (string.IsNullOrWhiteSpace(status)) return null;
        if (!Enum.TryParse<OrchestrationInstanceStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
            return "The instance status is invalid.";
        selectedStatus = parsed;
        return null;
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
