namespace Krackend.Sagas.Orchestrations.SchemaRegistry;

/// <summary>
/// Selects a schema contract resolver for a contract reference.
/// </summary>
public interface ISchemaContractResolverCatalog
{
    /// <summary>
    /// Gets the resolver registered for the supplied reference.
    /// </summary>
    ISchemaContractResolver GetResolver(SchemaContractReference reference);
}
