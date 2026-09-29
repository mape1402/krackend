namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Renders CSS variables from Krackend orchestration Web UI theme options.
/// </summary>
public interface IOrchestratorThemeCssRenderer
{
    /// <summary>
    /// Renders CSS rules that expose the configured light and dark palettes.
    /// </summary>
    /// <param name="options">Theme options to render.</param>
    /// <returns>CSS rules containing the theme variable declarations.</returns>
    string RenderThemeRules(OrchestratorWebUIThemeOptions options);
}
