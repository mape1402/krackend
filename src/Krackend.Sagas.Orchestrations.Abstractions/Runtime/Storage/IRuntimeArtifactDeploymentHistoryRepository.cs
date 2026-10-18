namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

/// <summary>
/// Persists the chronological history of deployed control-plane artifacts.
/// </summary>
public interface IRuntimeArtifactDeploymentHistoryRepository
{
    /// <summary>Appends an immutable runtime deployment history entry.</summary>
    Task Append(RuntimeArtifactDeploymentRecord record, CancellationToken cancellationToken = default);

    /// <summary>Reads deployment history in newest-first order.</summary>
    Task<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>> GetAll(CancellationToken cancellationToken = default);
}
