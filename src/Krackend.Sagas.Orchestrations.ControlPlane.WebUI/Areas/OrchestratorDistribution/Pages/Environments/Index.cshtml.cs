using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Environments;

public sealed class IndexModel : PageModel
{
    private readonly IDistributionEnvironmentApplicationService _service;

    public IndexModel(IDistributionEnvironmentApplicationService service)
    {
        _service = service;
    }

    public IReadOnlyCollection<DistributionEnvironmentModel> Rows { get; private set; } = Array.Empty<DistributionEnvironmentModel>();

    [BindProperty]
    public EnvironmentInput Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAll(new ApplicationPagedSettings { PageNumber = 1, PageSize = 200 }, cancellationToken);
        Rows = result.Rows;
    }

    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync(cancellationToken);
            return Page();
        }

        try
        {
            await _service.Upsert(new UpsertDistributionEnvironmentInput(
                Input.EnvironmentId,
                Input.Name.Trim(),
                Input.Code.Trim(),
                Input.Description?.Trim() ?? string.Empty,
                Input.IsEnabled), cancellationToken);
            SetMessage("Environment saved", "The distribution environment was saved.", "success");
        }
        catch (Exception ex)
        {
            SetMessage("Environment save failed", ex.Message, "error");
        }

        return RedirectToPage();
    }

    private void SetMessage(string title, string body, string type)
    {
        TempData["OrchestratorMessage.Title"] = title;
        TempData["OrchestratorMessage.Body"] = body;
        TempData["OrchestratorMessage.Type"] = type;
    }
}
