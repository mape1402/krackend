using System.Text.Json;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal sealed class DefaultIngressConfigurationAccessor :
        IGetAllIngressConfigurationsAccessor,
        IGetIngressConfigurationByArtifactAccessor
    {
        private static readonly IngressConfigurationReadingResult EmptyResult = new()
        {
            HasMoreItems = false,
            Configurations = [
                new IngressConfiguration{
                        ArtifactId = "123456789",
                        Id = Guid.NewGuid().ToString(),
                        IngressKind = IngressKind.Trigger,
                        IngressTransport = IngressTransport.Messaging,
                        SettingsPayload = JsonSerializer.Serialize(new { Topic = "events.sales.sale.created", Version = "1.0.0" })
                    },
                new IngressConfiguration{
                        ArtifactId = "123456789",
                        Id = Guid.NewGuid().ToString(),
                        IngressKind = IngressKind.Backchannel,
                        IngressTransport = IngressTransport.Messaging,
                        SettingsPayload = JsonSerializer.Serialize(new { Topic = "orchestrations.sales.sale.created", Version = "1.0.0" })
                    }
                ]
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
