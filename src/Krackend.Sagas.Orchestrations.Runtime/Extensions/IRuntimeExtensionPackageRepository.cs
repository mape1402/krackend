namespace Krackend.Sagas.Orchestrations.Runtime.Extensions;

using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

/// <summary>
/// Persists extension packages known by a runtime node.
/// </summary>
public interface IRuntimeExtensionPackageRepository
{
    /// <summary>
    /// Creates or updates an extension package.
    /// </summary>
    /// <param name="package">Runtime extension package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(RuntimeExtensionPackage package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all extension packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Runtime extension packages.</returns>
    Task<IReadOnlyCollection<RuntimeExtensionPackage>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active package matching an extension key and version.
    /// </summary>
    /// <param name="extensionKey">Extension key.</param>
    /// <param name="version">Extension version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active extension package, or null.</returns>
    Task<RuntimeExtensionPackage> TryGetActiveAsync(
        string extensionKey,
        SemanticVersion version,
        CancellationToken cancellationToken = default);
}

