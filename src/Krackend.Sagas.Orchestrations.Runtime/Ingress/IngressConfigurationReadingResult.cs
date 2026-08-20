namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public class IngressConfigurationReadingResult
    {
        public bool HasMoreItems { get; init; }

        public IReadOnlyCollection<IngressConfiguration> Configurations { get; init; }
    }
}
