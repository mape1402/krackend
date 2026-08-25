namespace Krackend.Sagas.Orchestrations.Runtime.Gossip;

/// <summary>
/// Configures runtime gossip notifications.
/// </summary>
public sealed class RuntimeGossipOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether gossip notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the gossip channel name.
    /// </summary>
    public string ChannelName { get; set; } = "krackend.runtime.artifacts.ready";

    /// <summary>
    /// Gets or sets the Redis connection string used by the Redis gossip adapter.
    /// </summary>
    public string RedisConnectionString { get; set; }
}
