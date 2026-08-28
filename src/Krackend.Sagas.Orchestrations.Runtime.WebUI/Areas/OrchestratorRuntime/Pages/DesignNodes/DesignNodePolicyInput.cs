using System.ComponentModel.DataAnnotations;
using Krackend.Sagas.Orchestrations.Abstractions.Distribution.Security;

namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Areas.OrchestratorRuntime.Pages.DesignNodes;

/// <summary>
/// Captures the distribution policy selected for a Runtime-side Design node registration.
/// </summary>
public sealed class DesignNodePolicyInput
{
    /// <summary>
    /// Gets or sets the design node id.
    /// </summary>
    [Required]
    public string DesignNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected distribution mode.
    /// </summary>
    [Required]
    [Display(Name = "Distribution mode")]
    public DistributionConnectionMode DistributionMode { get; set; } = DistributionConnectionMode.HybridSync;
}
