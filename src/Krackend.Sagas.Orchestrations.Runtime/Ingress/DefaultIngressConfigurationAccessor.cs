namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal sealed class DefaultIngressConfigurationAccessor :
        IGetAllIngressConfigurationsAccessor,
        IGetIngressConfigurationByArtifactAccessor
    {
        private static readonly IngressConfigurationReadingResult EmptyResult = new()
        {
            HasMoreItems = false,
            Configurations = []
        };

        public Task<IngressConfigurationReadingResult> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(EmptyResult);

        public Task<IReadOnlyCollection<IngressConfiguration>> GetConfigurationAsync(
            string artifactId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<IngressConfiguration>>([]);

        public void Dispose()
        {
        }
    }
}
