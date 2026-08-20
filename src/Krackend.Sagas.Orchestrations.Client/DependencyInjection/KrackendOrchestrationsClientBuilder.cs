namespace Krackend.Sagas.Orchestrations.Client.DependencyInjection;

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
}
