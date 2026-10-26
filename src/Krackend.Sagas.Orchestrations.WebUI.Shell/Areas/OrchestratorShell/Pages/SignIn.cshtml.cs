using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Areas.OrchestratorShell.Pages;

/// <summary>Hosts the presentation-only Zoorce sign-in page.</summary>
[AllowAnonymous]
public sealed class SignInModel : PageModel
{
    /// <summary>Renders the host-configured sign-in presentation.</summary>
    public void OnGet()
    {
    }
}
