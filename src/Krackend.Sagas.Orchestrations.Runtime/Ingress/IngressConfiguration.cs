namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public class IngressConfiguration
    {
        public string Id { get; set; }

        public string ArtifactId { get; set; }

        public IngressTransport IngressTransport { get; set; }

        public string SettingsPayload { get; set; }

        public IngressKind IngressKind { get; set; }
    }
}
