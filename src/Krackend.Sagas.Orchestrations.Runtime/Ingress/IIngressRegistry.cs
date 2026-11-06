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

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>Prepares ingress for a projected rollout without making it routable.</summary>
    public interface IStagedIngressRegistry
    {
        /// <summary>Starts the connector for a pending artifact generation.</summary>
        Task StandUpStagedAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default);
    }

    /// <summary>Prepares the selected connectors of an existing rollback target.</summary>
    public interface IRollbackIngressRegistry
    {
        /// <summary>Checks target trigger and backchannel connectors without changing routing.</summary>
        Task<IRollbackIngressPreparation> PrepareRollbackAsync(string artifactId, long ingressGeneration, IReadOnlyCollection<string> triggerKeys, CancellationToken cancellationToken = default);
    }

    /// <summary>Refreshes connector registrations after active ingress changes.</summary>
    public interface IRefreshIngressRegistry
    {
        /// <summary>Reads current ready configurations even when the generation was applied before.</summary>
        Task RefreshOneAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default);
    }

    /// <summary>Prepares archived backchannels before reactivating a recovered instance.</summary>
    public interface IArchivedRecoveryIngressRegistry
    {
        /// <summary>Prepares only backchannels and verifies their transport readiness.</summary>
        Task<IArchivedRecoveryIngressPreparation> PrepareArchivedRecoveryAsync(string artifactId, long ingressGeneration, CancellationToken cancellationToken = default);
    }

    /// <summary>Reconciles local connectors after an archived recovery is retired.</summary>
    public interface IArchivedRecoveryIngressCleanup
    {
        /// <summary>Returns artifact ids that currently own local connectors.</summary>
        IReadOnlyCollection<string> GetLocalArtifactIds();

        /// <summary>Disconnects local connectors only if the artifact is still retired and archived.</summary>
        Task<bool> ShutDownIfArchivedAsync(string artifactId, CancellationToken cancellationToken = default);
    }

    /// <summary>Owns the connectors created while preparing an archived recovery.</summary>
    public interface IArchivedRecoveryIngressPreparation : IAsyncDisposable
    {
        /// <summary>Retains the prepared connectors after activation commits.</summary>
        void Complete();
    }

    /// <summary>Owns only the local connectors created during rollback preparation.</summary>
    public interface IRollbackIngressPreparation : IAsyncDisposable
    {
        /// <summary>Keeps the prepared connectors after the handoff commits.</summary>
        void Complete();
    }
}
