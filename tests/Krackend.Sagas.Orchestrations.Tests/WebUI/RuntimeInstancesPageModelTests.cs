namespace Krackend.Sagas.Orchestrations.Tests.WebUI;

using Krackend.Sagas.Orchestrations.Runtime.WebUI;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public sealed class RuntimeInstancesPageModelTests
{
    [Fact]
    public async Task OnGetLoadsSnapshotAndResolvesDefaultHubPath()
    {
        var reader = new FakeRuntimeDiagnosticsReader();
        var page = CreatePage(reader, routePrefix: "/runtime/");

        await page.OnGetAsync();

        Assert.Equal("/runtime/live", page.LiveHubPath);
        Assert.Single(page.Instances);
        Assert.Single(page.Traffic);
        Assert.Single(page.HourlyTraffic);
        Assert.Equal(1, page.Summary.Active);
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

    private sealed class FakeRuntimeDiagnosticsReader : IRuntimeDiagnosticsReader
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

        public Task<RuntimeDashboardSnapshotModel> GetSnapshot(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);

        public Task<RuntimeDashboardSummaryModel> GetSummary(CancellationToken cancellationToken = default)
            => Task.FromResult(_summary);

        public Task<InstanceDetailModel> GetDetail(string instanceId, CancellationToken cancellationToken = default)
        {
            LastDetailInstanceId = instanceId;
            return Task.FromResult(_detail);
        }
    }
}
