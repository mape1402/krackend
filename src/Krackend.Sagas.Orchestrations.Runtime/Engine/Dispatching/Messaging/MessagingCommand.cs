using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    /// <summary>
    /// Describes a command that the runtime dispatches through messaging.
    /// </summary>
    public class MessagingCommand
    {
        /// <summary>
        /// Gets or sets the messaging topic where the command is published.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Gets or sets the messaging contract version used by the command.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the serialized business payload for the command.
        /// </summary>
        public string Payload { get; set; }
    }
}
