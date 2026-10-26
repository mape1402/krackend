using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Areas.OrchestratorShell.Pages;

/// <summary>Renders the supported standalone Zoorce HTTP status pages.</summary>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StatusModel : PageModel
{
    private static readonly IReadOnlyDictionary<int, string> Titles = new Dictionary<int, string>
    {
        [401] = "Authentication required",
        [403] = "Access denied",
        [404] = "Page not found",
        [500] = "Unexpected error",
        [503] = "Service unavailable"
    };

    /// <summary>Gets the response code shown by the page.</summary>
    public int Code { get; private set; }

    /// <summary>Gets the host-approved page title.</summary>
    public string Title { get; private set; } = "Something went wrong";

    /// <summary>Returns the requested status without exposing exception details.</summary>
    public IActionResult OnGet(int code)
    {
        if (!Titles.TryGetValue(code, out var title))
        {
            return NotFound();
        }

        Code = code;
        Title = title;
        Response.StatusCode = code;
        return Page();
    }
}
