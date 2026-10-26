using Krackend.Sagas.Orchestrations.Abstractions.Distribution;

namespace Krackend.Sagas.Orchestrations.Runtime.Distribution;

/// <summary>Indicates that an accepted deployment could not be acknowledged to its source.</summary>
public sealed class ArtifactPullAcknowledgementException : Exception
{
    /// <summary>Creates an acknowledgement failure carrying the accepted deployment.</summary>
    public ArtifactPullAcknowledgementException(RuntimeArtifactDeploymentResult deployment, Exception innerException)
        : base("The runtime accepted the release, but the control plane could not be notified.", innerException)
        => Deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));

    /// <summary>Gets the deployment already accepted by the runtime.</summary>
    public RuntimeArtifactDeploymentResult Deployment { get; }
}
