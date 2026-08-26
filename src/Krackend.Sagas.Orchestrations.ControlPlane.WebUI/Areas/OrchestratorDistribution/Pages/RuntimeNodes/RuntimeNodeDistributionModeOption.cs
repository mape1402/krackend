namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.RuntimeNodes;

/// <summary>
/// Represents one user-facing distribution policy option.
/// </summary>
public sealed class RuntimeNodeDistributionModeOption
{
    /// <summary>
    /// Gets or sets the runtime node distribution mode value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-facing distribution mode label.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-facing distribution mode description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
