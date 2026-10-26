using System.Text.Json;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Dashboard;

/// <summary>Renders an immediate dashboard shell backed by independently loaded statistics projections.</summary>
public sealed class IndexModel(IServiceProvider services, IOptions<OrchestratorRuntimeWebUIOptions> options) : PageModel
{
    public int Days { get; private set; } = 7;
    public string OrchestrationKey { get; private set; }
    public string LiveHubPath => $"/{(string.IsNullOrWhiteSpace(options.Value.RoutePrefix) ? "runtime" : options.Value.RoutePrefix.Trim('/'))}/live";

    /// <summary>Renders the shell without waiting for statistics queries.</summary>
    public Task OnGetAsync(int days = 7, string orchestrationKey = null)
    {
        Days = NormalizeDays(days);
        OrchestrationKey = NormalizeKey(orchestrationKey);
        return Task.CompletedTask;
    }

    /// <summary>Returns the independently loaded orchestration filter options.</summary>
    public async Task<IActionResult> OnGetFiltersAsync(CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(new { orchestrationKeys = await reader.GetOrchestrationKeysAsync(cancellationToken) });
    }

    /// <summary>Returns one cohort's current status distribution for dashboard counters and outcomes.</summary>
    public async Task<IActionResult> OnGetCohortAsync(int days = 7, string orchestrationKey = null,
        CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(await reader.GetCohortAsync(CreateQuery(days, orchestrationKey), cancellationToken));
    }

    /// <summary>Returns one independently refreshable activity timeline.</summary>
    public async Task<IActionResult> OnGetActivityAsync(int days = 7, string orchestrationKey = null,
        CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        var normalizedDays = NormalizeDays(days);
        var bucket = normalizedDays switch
        {
            1 => SagaStatisticsActivityBucket.Hour,
            90 => SagaStatisticsActivityBucket.Week,
            _ => SagaStatisticsActivityBucket.Day
        };
        var query = CreateQuery(normalizedDays, orchestrationKey);
        return Json(new
        {
            query.FromUtc,
            query.BeforeUtc,
            bucket,
            activity = await reader.GetActivityAsync(query, bucket, cancellationToken)
        });
    }

    /// <summary>Returns deferred projection freshness without coupling it to visible statistics.</summary>
    public async Task<IActionResult> OnGetHealthAsync(CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(await reader.GetHealthAsync(cancellationToken));
    }

    /// <summary>Preserves the former aggregate endpoint for callers outside the dashboard UI.</summary>
    public async Task<IActionResult> OnGetStatsAsync(int days = 7, string orchestrationKey = null,
        CancellationToken cancellationToken = default)
    {
        if (services.GetService(typeof(ISagaStatisticsReader)) is not ISagaStatisticsReader reader)
            return Unavailable();
        var query = CreateQuery(days, orchestrationKey);
        return Json(await reader.GetReportAsync(query.FromUtc, query.BeforeUtc, query.OrchestrationKey, cancellationToken));
    }

    private ISagaStatisticsDashboardReader GetDashboardReader()
        => services.GetService(typeof(ISagaStatisticsDashboardReader)) as ISagaStatisticsDashboardReader;

    private SagaStatisticsDashboardQuery CreateQuery(int days, string orchestrationKey)
    {
        var normalizedDays = NormalizeDays(days);
        var now = services.GetService(typeof(TimeProvider)) is TimeProvider clock
            ? clock.GetUtcNow().UtcDateTime
            : DateTime.UtcNow;
        var before = normalizedDays == 1
            ? new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1)
            : now.Date.AddDays(1);
        return new SagaStatisticsDashboardQuery(before.AddDays(-normalizedDays), before, NormalizeKey(orchestrationKey));
    }

    private IActionResult Unavailable()
        => StatusCode(503, "The configured runtime storage does not provide dashboard statistics.");

    private JsonResult Json(object value)
    {
        var serializer = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializer.Converters.Add(new JsonStringEnumConverter());
        return new JsonResult(value, serializer);
    }

    private static int NormalizeDays(int days) => days is 1 or 7 or 30 or 90 ? days : 7;

    private static string NormalizeKey(string orchestrationKey)
        => string.IsNullOrWhiteSpace(orchestrationKey) ? null : orchestrationKey.Trim();
}
