using System.ComponentModel.DataAnnotations;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>
/// Captures design node registration values in the runtime UI.
/// </summary>
public sealed class DesignNodeInput
{
    /// <summary>
    /// Gets or sets the design node identifier when editing.
    /// </summary>
    public string DesignNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime-local design node key.
    /// </summary>
    [Required(ErrorMessage = "Capture the design node key.")]
    [MaxLength(128)]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(?:[.-][A-Za-z0-9]+)*$", ErrorMessage = "Use letters or numbers separated by dot or dash, starting with a letter.")]
    [Display(Name = "Key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Required(ErrorMessage = "Capture the design node name.")]
    [MaxLength(256)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    [MaxLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;
}
