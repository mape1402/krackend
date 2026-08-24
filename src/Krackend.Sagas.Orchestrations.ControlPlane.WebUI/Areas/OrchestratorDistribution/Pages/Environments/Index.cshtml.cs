using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Environments;

public sealed class IndexModel : PageModel
{
    private readonly IRuntimeEnvironmentApplicationService _service;

    public IndexModel(IRuntimeEnvironmentApplicationService service)
    {
        _service = service;
    }

    public IReadOnlyCollection<RuntimeEnvironmentModel> Rows { get; private set; } = Array.Empty<RuntimeEnvironmentModel>();
    [BindProperty] public EnvironmentInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        Rows = (await _service.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 200 }, cancellationToken)).Rows;
    }

    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        await _service.Upsert(new UpsertRuntimeEnvironmentInput(Input.EnvironmentId, Input.Name, Input.Code, Input.Description), cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetEnabledAsync(string environmentId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await _service.SetEnabled(environmentId, isEnabled, cancellationToken);
        return RedirectToPage();
    }

    public sealed class EnvironmentInput
    {
        public string EnvironmentId { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

