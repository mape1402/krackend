namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Manages the lifecycle of ingress endpoints loaded by the current runtime replica.
    /// </summary>
    public interface IIngressRegistry
    {
        /// <summary>
        /// Stands up every ready ingress configuration visible to the current replica.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task StandUpAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stands up ingress configurations for one artifact generation.
        /// </summary>
        /// <param name="artifactId">Runtime artifact id.</param>
        /// <param name="ingressGeneration">Ingress generation expected by the local replica.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task StandUpOneAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Shuts down every ingress endpoint owned by the current runtime replica.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ShutDownAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Shuts down ingress endpoints for one runtime artifact.
        /// </summary>
        /// <param name="artifactId">Runtime artifact id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ShutDownOneAsync(string artifactId, CancellationToken cancellationToken = default);
    }
}
