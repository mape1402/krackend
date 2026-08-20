namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    public interface IGetAllIngressConfigurationsAccessor : IDisposable
    {
        Task<IngressConfigurationReadingResult> ReadAsync(CancellationToken cancellationToken = default);
    }
}
