namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Describes a transport command built by a task runtime adapter.
/// </summary>
public sealed class TaskRuntimeCommandDescriptor
{
    /// <summary>
    /// Gets or sets the transport used by the command.
    /// </summary>
    public RemoteCommandTransport Transport { get; set; }

    /// <summary>
    /// Gets or sets the transport-specific settings payload.
    /// </summary>
    public string SettingsPayload { get; set; }

    /// <summary>
    /// Gets or sets the optional reply address expected by callback-capable dispatches.
    /// </summary>
    public OrchestrationReplyAddress ReplyAddress { get; set; }
}
