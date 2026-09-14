using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Formats the implicit messaging topic used as orchestration backchannel for an artifact.
    /// </summary>
    public interface IBackchannelMessagingTopicFormatter
    {
        /// <summary>
        /// Formats the backchannel messaging topic for the specified artifact.
        /// </summary>
        /// <param name="artifact">Artifact that owns the backchannel.</param>
        /// <returns>The messaging topic used as backchannel.</returns>
        string Format(OrchestrationArtifact artifact);
    }
}
