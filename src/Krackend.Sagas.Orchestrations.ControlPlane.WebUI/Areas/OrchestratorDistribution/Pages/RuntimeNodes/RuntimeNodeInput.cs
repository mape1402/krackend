using System.ComponentModel.DataAnnotations;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;

namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Distribution.Areas.OrchestratorDistribution.Pages.RuntimeNodes;

/// <summary>
/// Captures runtime node values edited from the Design UI.
/// </summary>
public sealed class RuntimeNodeInput
{
    /// <summary>
    /// Gets or sets the runtime node id when editing.
    /// </summary>
    public string RuntimeNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime node display name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime node code.
    /// </summary>
    [Required]
    [MaxLength(128)]
    [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime environment id.
    /// </summary>
    [Required]
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets who can initiate artifact distribution.
    /// </summary>
    [Required]
    public DistributionMode DistributionMode { get; set; } = DistributionMode.HybridSync;

    /// <summary>
    /// Gets or sets the runtime endpoint base URI.
    /// </summary>
    [Url]
    [MaxLength(1024)]
    public string EndpointBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime node description.
    /// </summary>
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;
}
