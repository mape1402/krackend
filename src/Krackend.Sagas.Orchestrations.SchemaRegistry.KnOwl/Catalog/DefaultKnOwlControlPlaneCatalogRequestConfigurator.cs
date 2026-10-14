namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

internal sealed class DefaultKnOwlControlPlaneCatalogRequestConfigurator : IKnOwlControlPlaneCatalogRequestConfigurator
{
    public ValueTask ConfigureAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
