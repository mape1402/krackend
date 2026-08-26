using System.ComponentModel.DataAnnotations;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>
/// Captures a Design-generated credential package imported into Runtime.
/// </summary>
public sealed class RuntimeDesignNodeCredentialPackageInput
{
    /// <summary>
    /// Gets or sets the design node id.
    /// </summary>
    public string DesignNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON or Base64 credential package.
    /// </summary>
    [Required]
    public string Package { get; set; } = string.Empty;
}
