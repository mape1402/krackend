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
    /// Gets or sets the Design credentials JSON.
    /// </summary>
    [Required]
    public string CredentialsJson { get; set; } = string.Empty;
}
