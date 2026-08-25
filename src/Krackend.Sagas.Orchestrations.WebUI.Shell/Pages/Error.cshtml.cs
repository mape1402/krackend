using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Pages;

/// <summary>
/// Renders the shared fallback page for unhandled server errors.
/// </summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class ErrorModel : PageModel
{
    /// <summary>
    /// Gets the current request identifier when the server provides one.
    /// </summary>
    public string RequestId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the page can display a request identifier.
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);

    /// <summary>
    /// Initializes the request diagnostic information.
    /// </summary>
    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
