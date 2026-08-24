using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.RuntimeNodes;

public sealed class IndexModel : PageModel
{
    private readonly IRuntimeNodeApplicationService _service;
    private readonly IRuntimeEnvironmentApplicationService _environmentService;

    public IndexModel(
        IRuntimeNodeApplicationService service,
        IRuntimeEnvironmentApplicationService environmentService)
    {
        _service = service;
        _environmentService = environmentService;
    }

    public IReadOnlyCollection<RuntimeNodeModel> Rows { get; private set; } = Array.Empty<RuntimeNodeModel>();
    public IReadOnlyCollection<RuntimeEnvironmentModel> Environments { get; private set; } = Array.Empty<RuntimeEnvironmentModel>();
    [BindProperty] public RuntimeNodeInput Input { get; set; } = new();
    public IReadOnlyCollection<string> DistributionModes { get; } = Enum.GetNames<DistributionMode>();

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 100 }, cancellationToken);
        Rows = result.Rows;
        Environments = (await _environmentService.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 200 }, cancellationToken))
            .Rows
            .Where(x => x.IsEnabled)
            .ToArray();
    }

    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        await _service.Upsert(new UpsertRuntimeNodeInput(Input.RuntimeNodeId, Input.Name, Input.Code, Input.EnvironmentId,
            Input.DistributionMode, Input.EndpointBaseUri, Input.Description), cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetEnabledAsync(string runtimeNodeId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await _service.SetEnabled(runtimeNodeId, isEnabled, cancellationToken);
        return RedirectToPage();
    }

    public sealed class RuntimeNodeInput
    {
        public string RuntimeNodeId { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public string EnvironmentId { get; set; } = string.Empty;
        [Required] public DistributionMode DistributionMode { get; set; } = DistributionMode.Hybrid;
        public string EndpointBaseUri { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

