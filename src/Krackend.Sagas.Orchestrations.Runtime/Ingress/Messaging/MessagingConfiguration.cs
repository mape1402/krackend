namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging
{
    public class MessagingConfiguration
    {
        public string ConnectorId { get; set; }

        public string ArtifactId { get; set; }

        public string Topic { get; set; }

        public string Version { get; set; }

        public IngressKind MessageType { get; set; }
    }
}
