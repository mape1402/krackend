namespace Krackend.Sagas.Orchestrations.Client.DependencyInjection;

using Krackend.Sagas.Orchestrations.Client.Errors;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builds Krackend orchestration client integrations.
/// </summary>
public sealed class KrackendOrchestrationsClientBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KrackendOrchestrationsClientBuilder"/> class.
    /// </summary>
    public KrackendOrchestrationsClientBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Configures the global mapping between client exceptions and orchestration error codes.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The same builder instance.</returns>
    public KrackendOrchestrationsClientBuilder ConfigureErrorMapping(
        Action<OrchestrationClientErrorMappingOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        Services.Configure(configure);
        return this;
    }

    /// <summary>
    /// Maps a client exception type to an orchestration error code.
    /// </summary>
    /// <typeparam name="TException">Exception type matched by the mapping.</typeparam>
    /// <param name="errorCode">Error code understood by the orchestrator definition.</param>
    /// <param name="isRetryableCandidate">Optional retryability hint reported to the orchestrator.</param>
    /// <returns>The same builder instance.</returns>
    public KrackendOrchestrationsClientBuilder MapException<TException>(
        string errorCode,
        bool? isRetryableCandidate = null)
        where TException : Exception
        => ConfigureErrorMapping(options => options.Map<TException>(errorCode, isRetryableCandidate));

    /// <summary>
    /// Maps a client exception type to an orchestration error code when the predicate matches.
    /// </summary>
    /// <typeparam name="TException">Exception type matched by the mapping.</typeparam>
    /// <param name="errorCode">Error code understood by the orchestrator definition.</param>
    /// <param name="predicate">Predicate that must match the exception.</param>
    /// <param name="isRetryableCandidate">Optional retryability hint reported to the orchestrator.</param>
    /// <returns>The same builder instance.</returns>
    public KrackendOrchestrationsClientBuilder MapException<TException>(
        string errorCode,
        Func<TException, bool> predicate,
        bool? isRetryableCandidate = null)
        where TException : Exception
        => ConfigureErrorMapping(options => options.Map(errorCode, predicate, isRetryableCandidate));
}
