using System.ComponentModel.DataAnnotations;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.Environments;

/// <summary>
/// Captures distribution environment values edited from the Control Plane UI.
/// </summary>
public sealed class EnvironmentInput
{
    /// <summary>
    /// Gets or sets the environment id when editing.
    /// </summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment display name.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment code.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment description.
    /// </summary>
    [MaxLength(1024)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the environment is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
