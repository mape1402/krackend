namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Configures the cookie used to persist the selected orchestration Web UI color mode.
/// </summary>
public sealed class OrchestratorThemeModeCookieOptions
{
    /// <summary>
    /// Gets or sets the cookie name used to persist the selected color mode.
    /// </summary>
    public string CookieName { get; set; } = ".Krackend.Orchestrator.ThemeMode";

    /// <summary>
    /// Gets or sets how long the selected color mode should be retained.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(365);
}
