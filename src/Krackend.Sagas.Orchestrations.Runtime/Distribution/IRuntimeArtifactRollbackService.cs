using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>Requests a staged return to an already installed orchestration version.</summary>
public interface IRuntimeArtifactRollbackService
{
    /// <summary>Schedules rollback to an active, ready version without interrupting its existing callbacks.</summary>
    Task<RuntimeArtifactDeploymentResult> RollbackAsync(string artifactId, string requestedBy, CancellationToken cancellationToken = default);
}
