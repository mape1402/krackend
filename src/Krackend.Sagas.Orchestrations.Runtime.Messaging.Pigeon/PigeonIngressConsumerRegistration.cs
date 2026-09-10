namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

internal sealed record PigeonIngressConsumerRegistration(
    string ConnectorId,
    string EndpointKey,
    string Topic,
    string Version,
    string Subscription);
