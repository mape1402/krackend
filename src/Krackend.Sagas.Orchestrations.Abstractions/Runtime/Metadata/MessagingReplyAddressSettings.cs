namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

/// <summary>
/// Describes a messaging reply address inside an orchestration reply address payload.
/// </summary>
public sealed class MessagingReplyAddressSettings
{
    /// <summary>
    /// Gets or sets the messaging topic or queue name.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Gets or sets the messaging contract version.
    /// </summary>
    public string Version { get; set; }
}
