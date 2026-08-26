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
    [RegularExpression(@"^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", ErrorMessage = "Use lowercase segments separated by dot or dash, starting with a letter.")]
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
    /// Gets or sets the design node endpoint base URI.
    /// </summary>
    [Required(ErrorMessage = "Capture the design node endpoint.")]
    [MaxLength(1024)]
    [Url(ErrorMessage = "Capture a valid absolute URL.")]
    [Display(Name = "Endpoint base URL")]
    public string EndpointBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime node id assigned by the design node.
    /// </summary>
    [Required(ErrorMessage = "Capture the remote runtime node id.")]
    [MaxLength(128)]
    [Display(Name = "Remote runtime node id")]
    public string RemoteRuntimeNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signing client id.
    /// </summary>
    [MaxLength(256)]
    [Display(Name = "Client id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the imported credential key id.
    /// </summary>
    [MaxLength(256)]
    [Display(Name = "Key id")]
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the imported remote credential secret to create or replace.
    /// </summary>
    [MaxLength(2048)]
    [Display(Name = "Remote credential secret")]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    [MaxLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the node is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
