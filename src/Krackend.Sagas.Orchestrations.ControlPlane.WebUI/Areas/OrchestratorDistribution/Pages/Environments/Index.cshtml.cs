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

    [BindProperty]
    public EnvironmentInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnGetListAsync([FromQuery(Name = "page")] int page = 1, int pageSize = 25,
        string search = null, string sort = null, bool descending = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.Search(new ApplicationPagedSettings
            {
                PageNumber = Math.Max(page, 1), PageSize = Math.Clamp(pageSize, 1, 100)
            }, search?.Trim(), sort, descending, cancellationToken);
            return new JsonResult(new
            {
                items = result.Rows.Select(x => new
                {
                    id = x.Id, displayName = x.Name, key = x.Code, description = x.Description,
                    isActive = x.IsEnabled, status = x.IsEnabled ? "Enabled" : "Disabled",
                    updated = (x.UpdatedAtUtc ?? x.CreatedAtUtc).ToString("yyyy-MM-dd HH:mm:ss")
                }),
                totalCount = result.TotalRows, hasMore = (long)result.PageNumber * result.PageSize < result.TotalRows,
                search = search?.Trim() ?? string.Empty, sort = sort ?? string.Empty, descending
            });
        }
        catch (NotSupportedException)
        {
            return StatusCode(503, "The configured environment storage does not support catalog search.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(500, "Environments could not be loaded. Try again in a moment.");
        }
    }

    public async Task<IActionResult> OnPostUpsertAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
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
