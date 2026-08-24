using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>
/// Installs artifact delivery packages into the runtime artifact store.
/// </summary>
public interface IRuntimeArtifactDeploymentService
{
    /// <summary>
    /// Installs a control-plane artifact delivery package.
    /// </summary>
    Task<RuntimeArtifactDeploymentResult> DeployAsync(
        RuntimeArtifactDeliveryPackage package,
        string sourceKey,
        CancellationToken cancellationToken = default);
}
