namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>
/// Represents one distribution mode option shown by the Runtime-side Design node wizard.
/// </summary>
public sealed class RuntimeDesignNodeDistributionModeOption
{
    /// <summary>
    /// Gets or sets the option value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
