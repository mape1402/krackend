namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    /// <summary>
    /// Contains transport-specific settings for a messaging ingress.
    /// </summary>
    public sealed class MessagingIngressSettings
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
}
