using Krackend.Sagas.Orchestrations.Abstractions.Artifacts;

namespace Krackend.Sagas.Orchestrations.Runtime.Ingress
{
    internal sealed class DefaultRuntimeIngressConfigurationKeyBuilder : IRuntimeIngressConfigurationKeyBuilder
    {
        public string BuildTriggerKey(TriggerBindingArtifact trigger)
        {
            if (trigger is null)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            return $"{IngressKind.Trigger}:{trigger.Id}".ToLowerInvariant();
        }

        public string BuildBackchannelKey(IngressTransport transport)
            => $"{IngressKind.Backchannel}:{transport}".ToLowerInvariant();
    }
}
