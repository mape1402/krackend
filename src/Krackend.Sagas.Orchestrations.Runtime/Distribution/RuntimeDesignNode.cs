using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Represents a design/control-plane node trusted by a runtime node for artifact distribution.
/// </summary>
public sealed class RuntimeDesignNode
{
    /// <summary>
    /// Gets or sets the runtime-local design node identifier.
    /// </summary>
    public Id Id { get; set; } = Id.New();

    /// <summary>
    /// Gets or sets the runtime-local key used to identify this design node.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the design node base URI.
    /// </summary>
    public string EndpointBaseUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime node identifier assigned by this design node.
    /// </summary>
    public string RemoteRuntimeNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key identifier used to sign pull requests and validate push requests.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secret reference used by artifact distribution services.
    /// </summary>
    public string SecretReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the protected shared secret value.
    /// </summary>
    public string ProtectedSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this design node can be used by runtime distribution.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when the node was registered.
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets when the node was last updated.
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the last UTC instant when connectivity was checked.
    /// </summary>
    public DateTime? LastConnectionCheckedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the last connectivity check succeeded.
    /// </summary>
    public bool? LastConnectionSucceeded { get; set; }

    /// <summary>
    /// Gets or sets the last connectivity check message.
    /// </summary>
    public string LastConnectionMessage { get; set; } = string.Empty;
}
