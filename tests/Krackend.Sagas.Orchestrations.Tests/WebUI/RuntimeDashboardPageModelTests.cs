using System.Text.Json;
using System.Text.Json.Serialization;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.Statistics;
using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Dashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

public sealed class RuntimeDashboardPageModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 16, 37, 0, TimeSpan.Zero);

    [Fact]
    public async Task OnGetRendersTheShellWithoutLoadingStatistics()
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        await page.OnGetAsync(30, " sales.sale.created ");

        Assert.Equal(30, page.Days);
        Assert.Equal("sales.sale.created", page.OrchestrationKey);
        Assert.Equal("/runtime/live", page.LiveHubPath);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public void LiveHubPathUsesTheDefaultPrefixWhenTheHostDoesNotConfigureOne()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var page = new IndexModel(services, Options.Create(new OrchestratorRuntimeWebUIOptions { RoutePrefix = null! }));

        Assert.Equal("/runtime/live", page.LiveHubPath);
    }

    [Theory]
    [InlineData(1, "Hour")]
    [InlineData(7, "Day")]
    [InlineData(30, "Day")]
    [InlineData(90, "Week")]
    [InlineData(4, "Day")]
    public async Task ActivityUsesTheAppropriateUtcBucket(int days, string expectedBucket)
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        var result = Assert.IsType<JsonResult>(await page.OnGetActivityAsync(days, " sales.sale.created "));

        Assert.Equal(expectedBucket, reader.ActivityBucket.ToString());
        Assert.Equal(days is 1 or 7 or 30 or 90 ? days : 7, (reader.ActivityQuery.BeforeUtc - reader.ActivityQuery.FromUtc).TotalDays);
        Assert.Equal("sales.sale.created", reader.ActivityQuery.OrchestrationKey);
        Assert.Contains($"\"bucket\":\"{expectedBucket}\"", Serialize(result.Value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public async Task ActivityUsesMinuteBucketsOnlyForTheOneHourDashboardRange(int hours)
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        await page.OnGetActivityAsync(hours: hours);

        Assert.Equal(hours == 1 ? SagaStatisticsActivityBucket.Minute : SagaStatisticsActivityBucket.Hour, reader.ActivityBucket);
        Assert.Equal(hours, (reader.ActivityQuery.BeforeUtc - reader.ActivityQuery.FromUtc).TotalHours);
    }

    [Fact]
    public async Task ActivityUsesARollingWindowInsteadOfRoundingAtTheUtcHour()
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        await page.OnGetActivityAsync(hours: 1);

        Assert.Equal(Now.UtcDateTime, reader.ActivityQuery.BeforeUtc);
        Assert.Equal(Now.UtcDateTime.AddHours(-1), reader.ActivityQuery.FromUtc);
        Assert.Equal(SagaStatisticsActivityBucket.Minute, reader.ActivityBucket);
    }

    [Fact]
    public async Task OnGetPrefersASupportedHourlyRange()
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        await page.OnGetAsync(hours: 6);

        Assert.Equal(6, page.Hours);
    }

    [Fact]
    public async Task CohortFiltersHealthAndOptionsUseIndependentReaderCalls()
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        var filters = Assert.IsType<JsonResult>(await page.OnGetFiltersAsync());
        var cohort = Assert.IsType<JsonResult>(await page.OnGetCohortAsync(1, "sales.sale.created"));
        var health = Assert.IsType<JsonResult>(await page.OnGetHealthAsync());

        Assert.Contains("sales.sale.created", Serialize(filters.Value));
        Assert.Contains("sales.sale.updated", Serialize(filters.Value));
        var value = Assert.IsType<SagaStatisticsDashboardCohort>(cohort.Value);
        Assert.Equal(20, value.Total);
        Assert.Equal(19, value.CurrentStatuses[OrchestrationInstanceStatus.Completed]);
        Assert.Equal(1, value.CurrentStatuses[OrchestrationInstanceStatus.Running]);
        Assert.IsType<SagaStatisticsDashboardHealth>(health.Value);
        Assert.Equal(1, reader.FilterCalls);
        Assert.Equal(1, reader.CohortCalls);
        Assert.Equal(1, reader.HealthCalls);
        Assert.Equal(0, reader.ActivityCalls);
    }

    [Fact]
    public async Task SuggestionEndpointsStayBoundedAndVersionFlowsIntoDashboardQueries()
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        var keys = Assert.IsType<JsonResult>(await page.OnGetOrchestrationSuggestionsAsync(" sale ", 999));
        var versions = Assert.IsType<JsonResult>(await page.OnGetVersionSuggestionsAsync(" sales.sale.created ", "2", 999));
        await page.OnGetCohortAsync(hours: 6, orchestrationKey: " sales.sale.created ", orchestrationVersion: " 2.0.0 ");

        Assert.Contains("sales.sale.created", Serialize(keys.Value));
        Assert.Contains("2.0.0", Serialize(versions.Value));
        Assert.Equal("sale", reader.KeySuggestionSearch);
        Assert.Equal(50, reader.KeySuggestionTake);
        Assert.Equal("sales.sale.created", reader.VersionSuggestionKey);
        Assert.Equal("2", reader.VersionSuggestionSearch);
        Assert.Equal(50, reader.VersionSuggestionTake);
        Assert.Equal("sales.sale.created", reader.CohortQuery.OrchestrationKey);
        Assert.Equal("2.0.0", reader.CohortQuery.OrchestrationVersion);
    }

    [Fact]
    public async Task TopOrchestrationsUsesTheSelectedPeriodAndOneOfTheSupportedLimits()
    {
        var reader = new FakeDashboardReader();
        using var services = CreateServices(reader);
        var page = CreatePage(services);

        var result = Assert.IsType<JsonResult>(await page.OnGetTopOrchestrationsAsync(hours: 6, limit: 25));

        Assert.Equal(6, (reader.TopOrchestrationsQuery.BeforeUtc - reader.TopOrchestrationsQuery.FromUtc).TotalHours);
        Assert.Equal(25, reader.TopOrchestrationsLimit);
        Assert.Contains("sales.sale.created", Serialize(result.Value));

        await page.OnGetTopOrchestrationsAsync(hours: 6, limit: 999);

        Assert.Equal(10, reader.TopOrchestrationsLimit);
    }

    [Fact]
    public async Task DashboardHandlersReportUnavailableWhenTheProjectionReaderIsNotRegistered()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var page = CreatePage(services);

        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetFiltersAsync()).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetCohortAsync()).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetActivityAsync()).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetTopOrchestrationsAsync()).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetHealthAsync()).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetStatsAsync()).StatusCode);
    }

    [Fact]
    public async Task LegacyStatsEndpointRemainsAvailableWithoutTheDashboardProjectionReader()
    {
        var reader = new FakeLegacyReader();
        using var services = new ServiceCollection()
            .AddSingleton<ISagaStatisticsReader>(reader)
            .AddSingleton<TimeProvider>(new FixedTimeProvider(Now))
            .BuildServiceProvider();
        var page = CreatePage(services);

        var result = Assert.IsType<JsonResult>(await page.OnGetStatsAsync(7, " sales.sale.created "));

        Assert.IsType<SagaStatisticsReport>(result.Value);
        Assert.Equal(1, reader.CallCount);
        Assert.Equal("sales.sale.created", reader.OrchestrationKey);
        Assert.Equal(7, (reader.BeforeUtc - reader.FromUtc).TotalDays);
    }

    [Fact]
    public async Task ActivityAcceptsAnEmptyKeyWithoutAnInjectedClock()
    {
        var reader = new FakeDashboardReader();
        using var services = new ServiceCollection()
            .AddSingleton<ISagaStatisticsDashboardReader>(reader)
            .BuildServiceProvider();
        var page = CreatePage(services);

        await page.OnGetActivityAsync(1, " ");

        Assert.Null(reader.ActivityQuery.OrchestrationKey);
        Assert.Equal(SagaStatisticsActivityBucket.Hour, reader.ActivityBucket);
    }

    [Fact]
    public void DashboardQueryNormalizesKeysAndRejectsAnInvalidRange()
    {
        var normalized = new SagaStatisticsDashboardQuery(Now.UtcDateTime, Now.UtcDateTime.AddHours(1), " sales ", " 1.2.3 ").Normalize();

        Assert.Equal("sales", normalized.OrchestrationKey);
        Assert.Equal("1.2.3", normalized.OrchestrationVersion);
        Assert.Equal(DateTimeKind.Utc, normalized.FromUtc.Kind);
        Assert.Throws<ArgumentException>(() => new SagaStatisticsDashboardQuery(Now.UtcDateTime, Now.UtcDateTime).Normalize());
        Assert.Throws<ArgumentException>(() => new SagaStatisticsDashboardQuery(Now.UtcDateTime, Now.UtcDateTime.AddHours(1), OrchestrationVersion: "1.2").Normalize());
    }

    private static IndexModel CreatePage(IServiceProvider services)
        => new(services, Options.Create(new OrchestratorRuntimeWebUIOptions { RoutePrefix = "runtime" }));

    private static ServiceProvider CreateServices(FakeDashboardReader reader)
        => new ServiceCollection()
            .AddSingleton<ISagaStatisticsDashboardReader>(reader)
            .AddSingleton<TimeProvider>(new FixedTimeProvider(Now))
            .BuildServiceProvider();

    private static string Serialize(object? value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(value, options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDashboardReader : ISagaStatisticsDashboardReader
    {
        public int FilterCalls { get; private set; }
        public int CohortCalls { get; private set; }
        public int ActivityCalls { get; private set; }
        public int TopOrchestrationsCalls { get; private set; }
        public int HealthCalls { get; private set; }
        public int CallCount => FilterCalls + CohortCalls + ActivityCalls + TopOrchestrationsCalls + HealthCalls;
        public SagaStatisticsDashboardQuery ActivityQuery { get; private set; } = new(Now.UtcDateTime, Now.UtcDateTime.AddHours(1));
        public SagaStatisticsDashboardQuery CohortQuery { get; private set; } = new(Now.UtcDateTime, Now.UtcDateTime.AddHours(1));
        public SagaStatisticsActivityBucket ActivityBucket { get; private set; }
        public SagaStatisticsDashboardQuery TopOrchestrationsQuery { get; private set; } = new(Now.UtcDateTime, Now.UtcDateTime.AddHours(1));
        public int TopOrchestrationsLimit { get; private set; }
        public string KeySuggestionSearch { get; private set; } = string.Empty;
        public int KeySuggestionTake { get; private set; }
        public string VersionSuggestionKey { get; private set; } = string.Empty;
        public string VersionSuggestionSearch { get; private set; } = string.Empty;
        public int VersionSuggestionTake { get; private set; }

        public Task<IReadOnlyCollection<string>> GetOrchestrationKeysAsync(CancellationToken cancellationToken = default)
        {
            FilterCalls++;
            return Task.FromResult<IReadOnlyCollection<string>>(["sales.sale.created", "sales.sale.updated"]);
        }

        public Task<IReadOnlyCollection<string>> SearchOrchestrationKeysAsync(string search, int take = 20,
            CancellationToken cancellationToken = default)
        {
            KeySuggestionSearch = search;
            KeySuggestionTake = take;
            return Task.FromResult<IReadOnlyCollection<string>>(["sales.sale.created"]);
        }

        public Task<IReadOnlyCollection<string>> SearchOrchestrationVersionsAsync(string orchestrationKey, string search,
            int take = 20, CancellationToken cancellationToken = default)
        {
            VersionSuggestionKey = orchestrationKey;
            VersionSuggestionSearch = search;
            VersionSuggestionTake = take;
            return Task.FromResult<IReadOnlyCollection<string>>(["2.0.0"]);
        }

        public Task<SagaStatisticsDashboardCohort> GetCohortAsync(SagaStatisticsDashboardQuery query,
            CancellationToken cancellationToken = default)
        {
            CohortCalls++;
            CohortQuery = query;
            return Task.FromResult(new SagaStatisticsDashboardCohort(query.FromUtc, query.BeforeUtc, query.OrchestrationKey, 20,
                new Dictionary<OrchestrationInstanceStatus, long>
                {
                    [OrchestrationInstanceStatus.Completed] = 19,
                    [OrchestrationInstanceStatus.Running] = 1
                }));
        }

        public Task<IReadOnlyCollection<SagaStatisticsDashboardActivity>> GetActivityAsync(SagaStatisticsDashboardQuery query,
            SagaStatisticsActivityBucket bucket, CancellationToken cancellationToken = default)
        {
            ActivityCalls++;
            ActivityQuery = query;
            ActivityBucket = bucket;
            return Task.FromResult<IReadOnlyCollection<SagaStatisticsDashboardActivity>>(
            [new SagaStatisticsDashboardActivity(query.FromUtc, OrchestrationInstanceStatus.Completed, 1)]);
        }

        public Task<IReadOnlyCollection<SagaStatisticsDashboardOrchestration>> GetTopOrchestrationsAsync(
            SagaStatisticsDashboardQuery query, int take, CancellationToken cancellationToken = default)
        {
            TopOrchestrationsCalls++;
            TopOrchestrationsQuery = query;
            TopOrchestrationsLimit = take;
            return Task.FromResult<IReadOnlyCollection<SagaStatisticsDashboardOrchestration>>(
            [new SagaStatisticsDashboardOrchestration("sales.sale.created", 20)]);
        }

        public Task<SagaStatisticsDashboardHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            HealthCalls++;
            return Task.FromResult(new SagaStatisticsDashboardHealth(0, true, Now.UtcDateTime));
        }
    }

    private sealed class FakeLegacyReader : ISagaStatisticsReader
    {
        public int CallCount { get; private set; }
        public DateTime FromUtc { get; private set; }
        public DateTime BeforeUtc { get; private set; }
        public string? OrchestrationKey { get; private set; }

        public Task<SagaStatisticsReport> GetReportAsync(DateTime fromUtc, DateTime beforeUtc, string orchestrationKey = null!,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            FromUtc = fromUtc;
            BeforeUtc = beforeUtc;
            OrchestrationKey = orchestrationKey;
            return Task.FromResult(new SagaStatisticsReport(fromUtc, beforeUtc, orchestrationKey, 0,
                new Dictionary<OrchestrationInstanceStatus, long>(), new Dictionary<OrchestrationInstanceStatus, long>(),
                Array.Empty<SagaStatisticsActivity>(), Array.Empty<SagaStatisticsOrchestration>(), 0, true, null));
        }
    }
}
