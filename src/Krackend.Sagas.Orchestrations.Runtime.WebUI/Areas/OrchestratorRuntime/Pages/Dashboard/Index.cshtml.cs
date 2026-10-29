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
    public int Hours { get; private set; } = 168;
    public string OrchestrationKey { get; private set; }
    public string LiveHubPath => $"/{(string.IsNullOrWhiteSpace(options.Value.RoutePrefix) ? "runtime" : options.Value.RoutePrefix.Trim('/'))}/live";

    /// <summary>Renders the shell without waiting for statistics queries.</summary>
    public Task OnGetAsync(int days = 7, string orchestrationKey = null, int? hours = null)
    {
        Days = NormalizeDays(days);
        Hours = NormalizeHours(hours, days);
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

    /// <summary>Returns a bounded search result for a statistics orchestration picker.</summary>
    public async Task<IActionResult> OnGetOrchestrationSuggestionsAsync(string search = "", int take = 20,
        CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(new { items = await reader.SearchOrchestrationKeysAsync(search?.Trim() ?? string.Empty, NormalizeSuggestionTake(take), cancellationToken) });
    }

    /// <summary>Returns versions for the orchestration selected in a statistics picker.</summary>
    public async Task<IActionResult> OnGetVersionSuggestionsAsync(string orchestrationKey, string search = "", int take = 20,
        CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(new { items = await reader.SearchOrchestrationVersionsAsync(NormalizeKey(orchestrationKey), search?.Trim() ?? string.Empty, NormalizeSuggestionTake(take), cancellationToken) });
    }

    /// <summary>Returns one cohort's current status distribution for dashboard counters and outcomes.</summary>
    public async Task<IActionResult> OnGetCohortAsync(int days = 7, string orchestrationKey = null, string orchestrationVersion = null, int? hours = null,
        CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(await reader.GetCohortAsync(CreateQuery(days, orchestrationKey, orchestrationVersion, hours), cancellationToken));
    }

    /// <summary>Returns one independently refreshable activity timeline.</summary>
    public async Task<IActionResult> OnGetActivityAsync(int days = 7, string orchestrationKey = null, string orchestrationVersion = null, int? hours = null,
        CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        var normalizedHours = NormalizeHours(hours, days);
        var bucket = normalizedHours switch
        {
            1 => SagaStatisticsActivityBucket.Minute,
            <= 24 => SagaStatisticsActivityBucket.Hour,
            2160 => SagaStatisticsActivityBucket.Week,
            _ => SagaStatisticsActivityBucket.Day
        };
        var query = CreateQuery(days, orchestrationKey, orchestrationVersion, hours);
        return Json(new
        {
            query.FromUtc,
            query.BeforeUtc,
            bucket,
            activity = await reader.GetActivityAsync(query, bucket, cancellationToken)
        });
    }

    /// <summary>Returns the independently loaded ranking of the most frequently started orchestrations.</summary>
    public async Task<IActionResult> OnGetTopOrchestrationsAsync(int days = 7, string orchestrationKey = null, string orchestrationVersion = null, int? hours = null,
        int limit = 10, CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(await reader.GetTopOrchestrationsAsync(CreateQuery(days, orchestrationKey, orchestrationVersion, hours), NormalizeRankingLimit(limit), cancellationToken));
    }

    /// <summary>Returns deferred projection freshness without coupling it to visible statistics.</summary>
    public async Task<IActionResult> OnGetHealthAsync(CancellationToken cancellationToken = default)
    {
        var reader = GetDashboardReader();
        if (reader is null) return Unavailable();
        return Json(await reader.GetHealthAsync(cancellationToken));
    }

    /// <summary>Preserves the former aggregate endpoint for callers outside the dashboard UI.</summary>
    public async Task<IActionResult> OnGetStatsAsync(int days = 7, string orchestrationKey = null, string orchestrationVersion = null, int? hours = null,
        CancellationToken cancellationToken = default)
    {
        if (services.GetService(typeof(ISagaStatisticsReader)) is not ISagaStatisticsReader reader)
            return Unavailable();
        var query = CreateQuery(days, orchestrationKey, orchestrationVersion, hours);
        return Json(await reader.GetReportAsync(query.FromUtc, query.BeforeUtc, query.OrchestrationKey, cancellationToken));
    }

    private ISagaStatisticsDashboardReader GetDashboardReader()
        => services.GetService(typeof(ISagaStatisticsDashboardReader)) as ISagaStatisticsDashboardReader;

    private SagaStatisticsDashboardQuery CreateQuery(int days, string orchestrationKey, string orchestrationVersion = null, int? hours = null)
    {
        var normalizedHours = NormalizeHours(hours, days);
        var now = services.GetService(typeof(TimeProvider)) is TimeProvider clock
            ? clock.GetUtcNow().UtcDateTime
            : DateTime.UtcNow;
        // The dashboard is an operational, rolling view.  Do not round the upper
        // bound to an hour/day boundary: doing so would make "last hour" discard
        // all activity as soon as the clock crosses an hour.
        var before = now;
        return new SagaStatisticsDashboardQuery(before.AddHours(-normalizedHours), before, NormalizeKey(orchestrationKey), NormalizeVersion(orchestrationVersion));
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

    private static int NormalizeHours(int? hours, int days)
        => hours is 1 or 6 or 24 or 168 or 720 or 2160 ? hours.Value : NormalizeDays(days) * 24;

    private static int NormalizeRankingLimit(int limit) => limit is 10 or 15 or 20 or 25 ? limit : 10;

    private static int NormalizeSuggestionTake(int take) => Math.Clamp(take, 1, 50);

    private static string NormalizeKey(string orchestrationKey)
        => string.IsNullOrWhiteSpace(orchestrationKey) ? null : orchestrationKey.Trim();

    private static string NormalizeVersion(string orchestrationVersion)
        => string.IsNullOrWhiteSpace(orchestrationVersion) ? null : orchestrationVersion.Trim();
}
