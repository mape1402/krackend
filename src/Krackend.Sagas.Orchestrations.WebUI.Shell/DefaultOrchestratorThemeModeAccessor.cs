using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Resolves the request theme mode from the configured default and the persisted user preference cookie.
/// </summary>
public sealed class DefaultOrchestratorThemeModeAccessor : IOrchestratorThemeModeAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OrchestratorThemeModeCookieOptions _cookieOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOrchestratorThemeModeAccessor"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP context accessor used to read the current request cookie.</param>
    /// <param name="cookieOptions">Theme mode cookie options.</param>
    public DefaultOrchestratorThemeModeAccessor(
        IHttpContextAccessor httpContextAccessor,
        IOptions<OrchestratorThemeModeCookieOptions> cookieOptions)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _cookieOptions = cookieOptions?.Value ?? throw new ArgumentNullException(nameof(cookieOptions));
    }

    /// <inheritdoc />
    public OrchestratorWebUIThemeMode GetMode(OrchestratorWebUIThemeOptions theme)
    {
        var fallback = theme?.Mode ?? OrchestratorWebUIThemeMode.Light;
        var cookieValue = _httpContextAccessor.HttpContext?.Request.Cookies[_cookieOptions.CookieName];

        if (string.Equals(cookieValue, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return OrchestratorWebUIThemeMode.Dark;
        }

        if (string.Equals(cookieValue, "light", StringComparison.OrdinalIgnoreCase))
        {
            return OrchestratorWebUIThemeMode.Light;
        }

        return fallback;
    }

    /// <inheritdoc />
    public string GetCssMode(OrchestratorWebUIThemeOptions theme)
        => GetMode(theme) == OrchestratorWebUIThemeMode.Dark ? "dark" : "light";
}
