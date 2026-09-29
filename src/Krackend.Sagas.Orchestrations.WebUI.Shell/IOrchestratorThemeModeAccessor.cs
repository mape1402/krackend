namespace Krackend.Sagas.Orchestrations.WebUI.Shell;

/// <summary>
/// Resolves the effective Web UI color mode for the current request.
/// </summary>
public interface IOrchestratorThemeModeAccessor
{
    /// <summary>
    /// Gets the effective color mode for the current request.
    /// </summary>
    /// <param name="theme">Theme options configured by the host.</param>
    /// <returns>The request color mode.</returns>
    OrchestratorWebUIThemeMode GetMode(OrchestratorWebUIThemeOptions theme);

    /// <summary>
    /// Gets the effective Bootstrap color mode value for the current request.
    /// </summary>
    /// <param name="theme">Theme options configured by the host.</param>
    /// <returns>A CSS color mode value.</returns>
    string GetCssMode(OrchestratorWebUIThemeOptions theme);
}
