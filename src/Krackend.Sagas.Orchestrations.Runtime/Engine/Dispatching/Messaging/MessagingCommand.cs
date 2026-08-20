using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Dispatching.Messaging
{
    public class MessagingCommand
    {
        public string Topic { get; set; }

        public string Version { get; set; }

        public string Payload { get; set; }
    }
}
