namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Transport;

/// <summary>
/// Describes how an orchestration input was received or how an output must be dispatched.
/// </summary>
public sealed class RuntimeTransportDescriptor
{
    /// <summary>
    /// Gets or sets the transport kind.
    /// </summary>
    public RuntimeTransportKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the transport address.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the semantic version used by the transport contract.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets transport headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the native transport message id when available.
    /// </summary>
    public string MessageId { get; set; }
}
