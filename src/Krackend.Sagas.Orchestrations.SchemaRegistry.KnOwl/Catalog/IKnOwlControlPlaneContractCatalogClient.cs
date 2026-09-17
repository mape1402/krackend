using KnOwl.Contracts.Artifacts;

namespace Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.Catalog;

/// <summary>
/// Reads deployed contract artifacts from a KnOwl Control Plane catalog.
/// </summary>
public interface IKnOwlControlPlaneContractCatalogClient
{
    /// <summary>
    /// Gets every deployed contract artifact exposed by the KnOwl Control Plane catalog.
    /// </summary>
    Task<KnOwlContractCatalogResult> GetAllDeployedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an exact deployed contract artifact by artifact type, topic, and version.
    /// </summary>
    Task<KnOwlContractCatalogResult> GetExactAsync(
        ContractArtifactType artifactType,
        string topic,
        string versionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest deployed contract artifact by artifact type and topic.
    /// </summary>
    Task<KnOwlContractCatalogResult> GetLatestAsync(
        ContractArtifactType artifactType,
        string topic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an exact deployed command contract with its request and optional reply artifacts.
    /// </summary>
    Task<KnOwlCommandContractCatalogResult> GetExactCommandAsync(
        string commandKey,
        string versionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest deployed command contract with its request and optional reply artifacts.
    /// </summary>
    Task<KnOwlCommandContractCatalogResult> GetLatestCommandAsync(
        string commandKey,
        CancellationToken cancellationToken = default);
}
