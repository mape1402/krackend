using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    /// <summary>
    /// Builds stable keys for projected runtime ingress configurations.
    /// </summary>
    public interface IRuntimeIngressConfigurationKeyBuilder
    {
        /// <summary>
        /// Builds the key for an explicit trigger ingress configuration.
        /// </summary>
        /// <param name="trigger">Trigger binding artifact used to create the ingress configuration.</param>
        /// <returns>The stable trigger ingress configuration key.</returns>
        string BuildTriggerKey(TriggerBindingArtifact trigger);

        /// <summary>
        /// Builds the key for the implicit orchestration backchannel ingress configuration.
        /// </summary>
        /// <param name="transport">Transport used by the backchannel ingress.</param>
        /// <returns>The stable backchannel ingress configuration key.</returns>
        string BuildBackchannelKey(IngressTransport transport);
    }
}
