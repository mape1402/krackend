using System.Collections.Concurrent;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal sealed class PigeonIngressConsumerRegistry : IPigeonIngressConsumerRegistry
    {
        private readonly ConcurrentDictionary<string, byte> _registeredEndpoints = new(StringComparer.OrdinalIgnoreCase);

        public bool TryBeginRegistration(string endpointKey)
            => _registeredEndpoints.TryAdd(endpointKey, 0);

        public void Forget(string endpointKey)
            => _registeredEndpoints.TryRemove(endpointKey, out _);
    }
}
