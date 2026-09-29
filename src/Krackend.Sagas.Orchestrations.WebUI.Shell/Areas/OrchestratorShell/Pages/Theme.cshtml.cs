using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell.Areas.OrchestratorShell.Pages;

/// <summary>
/// Persists the selected shell color mode and redirects back to the current page.
/// </summary>
public sealed class ThemeModel : PageModel
{
    private readonly OrchestratorThemeModeCookieOptions _cookieOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeModel"/> class.
    /// </summary>
    /// <param name="cookieOptions">Theme mode cookie options.</param>
    public ThemeModel(IOptions<OrchestratorThemeModeCookieOptions> cookieOptions)
    {
        _cookieOptions = cookieOptions?.Value ?? throw new ArgumentNullException(nameof(cookieOptions));
    }

    /// <summary>
    /// Stores the selected color mode for subsequent server-rendered requests.
    /// </summary>
    /// <param name="mode">Requested color mode.</param>
    /// <param name="returnUrl">Local URL to return to after storing the preference.</param>
    /// <returns>A redirect to the previous page.</returns>
    public IActionResult OnPost(string mode, string returnUrl)
    {
        if (string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "light", StringComparison.OrdinalIgnoreCase))
        {
            Response.Cookies.Append(
                _cookieOptions.CookieName,
                mode.ToLowerInvariant(),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.Add(_cookieOptions.Lifetime),
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });
        }

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
