namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public class IngressConfiguration
    {
        public string Id { get; set; }

        public string ArtifactId { get; set; }

        public IngressKind Kind { get; set; }

        public string SettingsPayload { get; set; }
    }
}
