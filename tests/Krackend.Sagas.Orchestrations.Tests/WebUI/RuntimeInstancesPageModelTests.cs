namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

public sealed class RuntimeInstancesPageModelTests
{
    [Fact]
    public async Task OnGetDoesNotLoadDashboardOrAllInstances()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, routePrefix: "/runtime/");

        await page.OnGetAsync();

        Assert.Equal("/runtime/live", page.LiveHubPath);
        Assert.Empty(page.Instances);
        Assert.False(reader.SnapshotRequested);
        Assert.False(reader.SummaryRequested);
        Assert.Empty(page.Traffic);
        Assert.Empty(page.HourlyTraffic);
    }

    [Theory]
    [InlineData("orchestrationLabel", RuntimeInstanceCatalogSort.Orchestration)]
    [InlineData("correlationId", RuntimeInstanceCatalogSort.CorrelationId)]
    [InlineData("sagaId", RuntimeInstanceCatalogSort.SagaId)]
    [InlineData("status", RuntimeInstanceCatalogSort.Status)]
    [InlineData("startedOnUtc", RuntimeInstanceCatalogSort.Started)]
    [InlineData("other", RuntimeInstanceCatalogSort.Started)]
    public async Task ListPassesBoundedQueryToStorage(string sort, RuntimeInstanceCatalogSort expected)
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");

        Assert.IsType<JsonResult>(await page.OnGetListAsync(2, 500, " sale ", sort, true, "Running"));

        Assert.Equal(new RuntimeInstanceCatalogQuery(2, 100, "sale", OrchestrationInstanceStatus.Running, expected, true), reader.LastQuery);
    }

    [Fact]
    public async Task ListUsesStartedDescendingQueueOrderAndRejectsInvalidStatus()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");

        Assert.IsType<JsonResult>(await page.OnGetListAsync(-1, 0));
        Assert.Equal(new RuntimeInstanceCatalogQuery(1, 1, string.Empty, null, RuntimeInstanceCatalogSort.Started, true), reader.LastQuery);
        Assert.IsType<BadRequestObjectResult>(await page.OnGetListAsync(status: "unknown"));
    }

    [Fact]
    public async Task ListPassesTimeRangeInUtcAndRejectsInvalidRange()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");
        var start = new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.FromHours(-6));
        var end = start.AddDays(1);

        Assert.IsType<JsonResult>(await page.OnGetListAsync(startedFrom: start, startedBefore: end, activeOnly: true));
        Assert.Equal(start.UtcDateTime, reader.LastQuery.StartedFromUtc);
        Assert.Equal(end.UtcDateTime, reader.LastQuery.StartedBeforeUtc);
        Assert.True(reader.LastQuery.ActiveOnly);
        Assert.IsType<BadRequestObjectResult>(await page.OnGetListAsync(startedFrom: end, startedBefore: start));
        Assert.IsType<BadRequestObjectResult>(await page.OnGetListAsync(startedFrom: start, startedBefore: start));
    }

    [Fact]
    public async Task RecentRangeIsBoundedAndCannotBeCombinedWithExplicitDates()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");
        var before = DateTime.UtcNow.AddHours(-6);

        Assert.IsType<JsonResult>(await page.OnGetListAsync(recentHours: 6));
        Assert.InRange(reader.LastQuery.StartedFromUtc.Value, before, DateTime.UtcNow.AddHours(-6));
        Assert.IsType<JsonResult>(await page.OnGetCountsAsync(recentHours: 24));
        Assert.InRange(reader.LastQuery.StartedFromUtc.Value, DateTime.UtcNow.AddHours(-24).AddSeconds(-2), DateTime.UtcNow.AddHours(-24));
        Assert.IsType<BadRequestObjectResult>(await page.OnGetListAsync(recentHours: 0));
        Assert.IsType<BadRequestObjectResult>(await page.OnGetCountsAsync(recentHours: 169));
        Assert.IsType<JsonResult>(await page.OnGetCountsAsync(recentHours: 720));
        Assert.IsType<JsonResult>(await page.OnGetCountsAsync(recentHours: 2160));
        Assert.IsType<BadRequestObjectResult>(await page.OnGetCountsAsync(startedFrom: DateTimeOffset.UtcNow, recentHours: 1));
    }

    [Fact]
    public async Task ListReportsUnavailableWhenStorageDoesNotSupportPaging()
    {
        var reader = new SummaryOnlyReader();
        var page = new IndexModel(reader, Options.Create(new OrchestratorRuntimeWebUIOptions()));

        var result = Assert.IsType<ObjectResult>(await page.OnGetListAsync());

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task ListReportsUnavailableWhenUnderlyingRepositoryDoesNotSupportPaging()
    {
        var reader = new FakeRuntimeDiagnosticsReader { PagingUnsupported = true };
        var page = CreatePage(reader, "runtime");

        var result = Assert.IsType<ObjectResult>(await page.OnGetListAsync());

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task CountsUseTheSameScopedFiltersAsTheCatalog()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");
        var start = new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.FromHours(-6));
        var end = start.AddHours(3);

        var result = Assert.IsType<JsonResult>(await page.OnGetCountsAsync(" sale ", "Running", start, end, true));

        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyDictionary<OrchestrationInstanceStatus, long>>(result.Value)[OrchestrationInstanceStatus.Running]);
        Assert.Equal(new RuntimeInstanceCatalogQuery(1, 1, "sale", OrchestrationInstanceStatus.Running,
            RuntimeInstanceCatalogSort.Started, true, start.UtcDateTime, end.UtcDateTime, true), reader.LastQuery);
        Assert.IsType<BadRequestObjectResult>(await page.OnGetCountsAsync(status: "missing"));
        Assert.IsType<BadRequestObjectResult>(await page.OnGetCountsAsync(startedFrom: end, startedBefore: start));
    }

    [Fact]
    public async Task CountsReportUnavailableWhenNotSupported()
    {
        var page = new IndexModel(new SummaryOnlyReader(), Options.Create(new OrchestratorRuntimeWebUIOptions()));
        Assert.Equal(503, Assert.IsType<ObjectResult>(await page.OnGetCountsAsync()).StatusCode);
        var reader = new FakeRuntimeDiagnosticsReader { PagingUnsupported = true };
        Assert.Equal(503, Assert.IsType<ObjectResult>(await CreatePage(reader, "runtime").OnGetCountsAsync()).StatusCode);
    }

    [Fact]
    public async Task JsonEndpointsReturnSnapshotSummaryAndDetail()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, routePrefix: "");

        var snapshot = await page.OnGetSnapshotAsync();
        var summary = await page.OnGetSummaryAsync();
        var detail = await page.OnGetDetailAsync("instance-1");
        var badRequest = await page.OnGetDetailAsync(" ");

        Assert.Equal("/runtime/live", page.LiveHubPath);
        Assert.IsType<JsonResult>(snapshot);
        Assert.IsType<JsonResult>(summary);
        Assert.IsType<JsonResult>(detail);
        Assert.IsType<BadRequestResult>(badRequest);
        Assert.Equal("instance-1", reader.LastDetailInstanceId);
        Assert.Equal("od-status-active", IndexModel.StatusClass("Completed"));
    }

    [Fact]
    public async Task DetailPageLoadsTheRequestedInstanceAndRejectsInvalidIds()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = new DetailModel(reader);
        var id = Ulid.NewUlid().ToString();

        Assert.IsType<PageResult>(await page.OnGetAsync(id));
        Assert.Equal(id, reader.LastDetailInstanceId);
        Assert.NotNull(page.Detail);
        Assert.IsType<NotFoundResult>(await page.OnGetAsync("invalid"));
        Assert.IsType<NotFoundResult>(await page.OnGetAsync(null));
        reader.DetailMissing = true;
        Assert.IsType<NotFoundResult>(await page.OnGetAsync(id));
    }

    [Theory]
    [InlineData(null, "execution")]
    [InlineData("overview", "overview")]
    [InlineData("activity", "activity")]
    [InlineData("EXECUTION", "execution")]
    [InlineData("data", "data")]
    [InlineData("compensations", "compensations")]
    public async Task DetailPageLoadsIndependentSections(string requested, string expected)
    {
        var page = new DetailModel(new FakeRuntimeDiagnosticsReader());
        Assert.IsType<PageResult>(await page.OnGetAsync(Ulid.NewUlid().ToString(), section: requested));
        Assert.Equal(expected, page.Section);
    }

    [Fact]
    public async Task DetailPageRejectsUnknownSectionAndMissingExecutionLevels()
    {
        var page = new DetailModel(new FakeRuntimeDiagnosticsReader());
        var id = Ulid.NewUlid().ToString();
        Assert.IsType<NotFoundResult>(await page.OnGetAsync(id, section: "unknown"));
        Assert.IsType<NotFoundResult>(await page.OnGetAsync(id, section: "stage", stageKey: "missing"));
        Assert.IsType<NotFoundResult>(await page.OnGetAsync(id, section: "task", stageKey: "stage-one", taskKey: "missing"));
        Assert.IsType<NotFoundResult>(await page.OnGetAsync(id, section: "attempt", stageKey: "stage-one", taskKey: "task-one", attempt: 2));
    }

    [Fact]
    public async Task DetailPageResolvesStageTaskAndAttemptIndependently()
    {
        var page = new DetailModel(new FakeRuntimeDiagnosticsReader());
        var id = Ulid.NewUlid().ToString();
        Assert.IsType<PageResult>(await page.OnGetAsync(id, section: "stage", stageKey: "stage-one"));
        Assert.Equal("stage-one", page.Stage.StageKey);
        Assert.IsType<PageResult>(await page.OnGetAsync(id, section: "task", stageKey: "stage-one", taskKey: "task-one"));
        Assert.Equal("task-one", page.Task.TaskKey);
        Assert.IsType<PageResult>(await page.OnGetAsync(id, section: "attempt", stageKey: "stage-one", taskKey: "task-one", attempt: 1));
        Assert.Equal(1, page.Attempt.AttemptNumber);
    }

    private static IndexModel CreatePage(
        FakeRuntimeDiagnosticsReader reader,
        string routePrefix)
        => new(
            reader,
            Options.Create(new OrchestratorRuntimeWebUIOptions { RoutePrefix = routePrefix }));

    private sealed class FakeRuntimeDiagnosticsReader : IRuntimeDiagnosticsReader, IRuntimeInstanceCatalogReader, IRuntimeInstanceStatusCountsReader
    {
        private readonly RuntimeDashboardSnapshotModel _snapshot;
        private readonly RuntimeDashboardSummaryModel _summary;
        private readonly InstanceDetailModel _detail;

        public FakeRuntimeDiagnosticsReader()
        {
            var now = DateTime.UtcNow;
            var row = new InstanceRowModel(
                "instance-1",
                "sales.sale.created",
                "1.0.0",
                "correlation-1",
                "saga-1",
                "execution-1",
                "Running",
                "badge-info",
                "stage-one",
                "task-one",
                now,
                now,
                null,
                null,
                null,
                string.Empty);
            var traffic = new TrafficPointModel(now, 1, 1, 0, 0);
            var summary = new RuntimeSummaryModel(1, 0, 0, 0, 0, 0, now.AddMinutes(-1), now.AddHours(-1));

            _snapshot = new RuntimeDashboardSnapshotModel(summary, [row], [traffic], [traffic]);
            _summary = new RuntimeDashboardSummaryModel(summary, [traffic], [traffic]);
            var attempt = new TaskAttemptDetailModel("attempt", 1, "Completed", now, null, now, null, null, "{}", "{}", null, null, null, "{}");
            var task = new TaskDetailModel("task", "stage", "task-one", "Messaging", "Sequential", "Completed", true, "correlation-1", now, null, now, null, null, 1, "{}", "{}", [attempt]);
            var stage = new StageDetailModel("stage", "stage-one", 1, "Completed", "badge-info", now, now, null, null, "{}", [task]);
            _detail = new InstanceDetailModel(
                row,
                [stage],
                [task],
                [],
                "{}",
                "{}",
                [],
                [],
                []);
        }

        public string LastDetailInstanceId { get; private set; } = string.Empty;
        public bool SnapshotRequested { get; private set; }
        public bool SummaryRequested { get; private set; }
        public bool PagingUnsupported { get; set; }
        public bool DetailMissing { get; set; }
        public RuntimeInstanceCatalogQuery LastQuery { get; private set; }

        public Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default)
        {
            SnapshotRequested = true;
            return Task.FromResult(_snapshot);
        }

        public Task<RuntimeInstancePageModel> GetInstances(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
        {
            if (PagingUnsupported) throw new NotSupportedException();
            LastQuery = query;
            return Task.FromResult(new RuntimeInstancePageModel([], 0, query.PageNumber, query.PageSize));
        }

        public Task<IReadOnlyDictionary<OrchestrationInstanceStatus, long>> GetStatusCounts(RuntimeInstanceCatalogQuery query, CancellationToken cancellationToken = default)
        {
            if (PagingUnsupported) throw new NotSupportedException();
            LastQuery = query;
            return Task.FromResult<IReadOnlyDictionary<OrchestrationInstanceStatus, long>>(
                new Dictionary<OrchestrationInstanceStatus, long> { [OrchestrationInstanceStatus.Running] = 2 });
        }

        public Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default)
        {
            SummaryRequested = true;
            return Task.FromResult(_summary);
        }

        public Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default)
        {
            LastDetailInstanceId = instanceId;
            if (DetailMissing) throw new KeyNotFoundException();
            return Task.FromResult(_detail);
        }
    }

    private sealed class SummaryOnlyReader : IRuntimeDiagnosticsReader
    {
        public Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
