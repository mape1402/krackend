namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Reads runtime artifacts for runtime hot paths without exposing persistence details.
/// </summary>
public interface IRuntimeArtifactCatalog
{
    /// <summary>
    /// Reads active deployable orchestration artifacts in pages.
    /// </summary>
    /// <param name="cursor">Current page cursor.</param>
    /// <param name="pageSize">Maximum number of artifacts to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active artifact page.</returns>
    Task<RuntimeArtifactPage> ReadActiveDeployments(
        RuntimeArtifactPageCursor cursor,
        int pageSize,
        CancellationToken cancellationToken = default);
}
