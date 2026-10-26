using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>Displays releases available from one design node.</summary>
public sealed class PendingReleasesModel : PageModel
{
    /// <summary>Gets the selected distribution source.</summary>
    [BindProperty(SupportsGet = true)]
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>Validates the source for this view.</summary>
    public IActionResult OnGet() => string.IsNullOrWhiteSpace(SourceKey) ? BadRequest() : Page();
}
