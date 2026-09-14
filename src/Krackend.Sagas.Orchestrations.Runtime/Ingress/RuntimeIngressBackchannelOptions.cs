namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Configures the implicit runtime backchannel ingress topic generation.
    /// </summary>
    public sealed class RuntimeIngressBackchannelOptions
    {
        /// <summary>
        /// Gets or sets the topic prefix used to create orchestration backchannel topics.
        /// </summary>
        public string TopicPrefix { get; set; } = "orchestrations";
    }
}
