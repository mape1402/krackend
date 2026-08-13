using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Runtime;
using Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.Instances;

public sealed class IndexModel : PageModel
{
    private readonly IRuntimeDiagnosticsReader _diagnosticsReader;
    private readonly RuntimeEnvironmentDescriptor _runtimeEnvironment;
    private readonly OrchestratorRuntimeWebUIOptions _options;

    public IndexModel(
        IRuntimeDiagnosticsReader diagnosticsReader,
        RuntimeEnvironmentDescriptor runtimeEnvironment,
        IOptions<OrchestratorRuntimeWebUIOptions> options)
    {
        _diagnosticsReader = diagnosticsReader;
        _runtimeEnvironment = runtimeEnvironment;
        _options = options.Value;
    }

    public string EnvironmentKey => _runtimeEnvironment.EnvironmentKey;

    public string LiveHubPath
    {
        get
        {
            var prefix = string.IsNullOrWhiteSpace(_options.RoutePrefix) ? "runtime" : _options.RoutePrefix.Trim('/');
            return $"/{prefix}/live";
        }
    }

    public IReadOnlyCollection<InstanceRowModel> Instances { get; private set; } = Array.Empty<InstanceRowModel>();

    public IReadOnlyCollection<TrafficPointModel> Traffic { get; private set; } = Array.Empty<TrafficPointModel>();

    public RuntimeSummaryModel Summary { get; private set; } = new(0, 0, 0, 0, 0, 0, DateTime.UtcNow, DateTime.UtcNow);

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _diagnosticsReader.GetSnapshot(cancellationToken);
        Instances = snapshot.Instances;
        Traffic = snapshot.Traffic;
        Summary = snapshot.Summary;
    }

    public async Task<IActionResult> OnGetSnapshotAsync(CancellationToken cancellationToken = default)
        => new JsonResult(await _diagnosticsReader.GetSnapshot(cancellationToken));

    public async Task<IActionResult> OnGetSummaryAsync(CancellationToken cancellationToken = default)
        => new JsonResult(await _diagnosticsReader.GetSummary(cancellationToken));

    public async Task<IActionResult> OnGetDetailAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return BadRequest();

        return new JsonResult(await _diagnosticsReader.GetDetail(instanceId, cancellationToken));
    }

    public static string StatusClass(string status)
        => RuntimeDiagnosticsReader.StatusClass(status);
}
