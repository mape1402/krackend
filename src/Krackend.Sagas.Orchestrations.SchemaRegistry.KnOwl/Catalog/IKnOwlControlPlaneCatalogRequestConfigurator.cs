namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// Allows hosts to enrich outbound KnOwl Control Plane catalog requests.
/// </summary>
public interface IKnOwlControlPlaneCatalogRequestConfigurator
{
    /// <summary>
    /// Configures one outbound KnOwl catalog HTTP request before it is sent.
    /// </summary>
    ValueTask ConfigureAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
