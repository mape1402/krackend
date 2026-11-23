using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

internal sealed record PigeonIngressConsumerRegistration(
    string ConnectorId,
    string EndpointKey,
    string Topic,
    string Version,
    string Subscription)
{
    public IngressConfiguration? Configuration { get; init; }
}
