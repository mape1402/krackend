namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;
using Krackend.Sagas.Orchestrations.Runtime.Diagnostics;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public sealed class RuntimeInstancesPageModelTests
{
    [Fact]
    public async Task OnGetLoadsSummaryWithoutLoadingAllInstances()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, routePrefix: "/runtime/");

        await page.OnGetAsync();

        Assert.Equal("/runtime/live", page.LiveHubPath);
        Assert.Empty(page.Instances);
        Assert.False(reader.SnapshotRequested);
        Assert.Single(page.Traffic);
        Assert.Single(page.HourlyTraffic);
        Assert.Equal(1, page.Summary.Active);
    }

    [Theory]
    [InlineData("orchestrationLabel", RuntimeInstanceCatalogSort.Orchestration)]
    [InlineData("correlationId", RuntimeInstanceCatalogSort.CorrelationId)]
    [InlineData("sagaId", RuntimeInstanceCatalogSort.SagaId)]
    [InlineData("status", RuntimeInstanceCatalogSort.Status)]
    [InlineData("startedOnUtc", RuntimeInstanceCatalogSort.Started)]
    [InlineData("other", RuntimeInstanceCatalogSort.Updated)]
    public async Task ListPassesBoundedQueryToStorage(string sort, RuntimeInstanceCatalogSort expected)
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");

        Assert.IsType<JsonResult>(await page.OnGetListAsync(2, 500, " sale ", sort, true, "Running"));

        Assert.Equal(new RuntimeInstanceCatalogQuery(2, 100, "sale", OrchestrationInstanceStatus.Running, expected, true), reader.LastQuery);
    }

    [Fact]
    public async Task ListUsesLatestFirstAndRejectsInvalidStatus()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, "runtime");

        Assert.IsType<JsonResult>(await page.OnGetListAsync(-1, 0));
        Assert.Equal(new RuntimeInstanceCatalogQuery(1, 1, string.Empty, null, RuntimeInstanceCatalogSort.Updated, true), reader.LastQuery);
        Assert.IsType<BadRequestObjectResult>(await page.OnGetListAsync(status: "unknown"));
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

    private static IndexModel CreatePage(
        FakeRuntimeDiagnosticsReader reader,
        string routePrefix)
        => new(
            reader,
            Options.Create(new OrchestratorRuntimeWebUIOptions { RoutePrefix = routePrefix }));

    private sealed class FakeRuntimeDiagnosticsReader : IRuntimeDiagnosticsReader, IRuntimeInstanceCatalogReader
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
            _detail = new InstanceDetailModel(
                row,
                [],
                [],
                [],
                "{}",
                "{}",
                [],
                [],
                []);
        }

        public string LastDetailInstanceId { get; private set; } = string.Empty;
        public bool SnapshotRequested { get; private set; }
        public bool PagingUnsupported { get; set; }
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

        public Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default)
            => Task.FromResult(_summary);

        public Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default)
        {
            LastDetailInstanceId = instanceId;
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
