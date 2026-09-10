namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Reads ready ingress configurations used during runtime replica startup.
    /// </summary>
    public interface IGetAllIngressConfigurationsAccessor : IDisposable
    {
        /// <summary>
        /// Reads ingress configurations ordered for immediate standup.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The ingress configuration page.</returns>
        Task<IngressConfigurationReadingResult> ReadAsync(CancellationToken cancellationToken = default);
    }
}
