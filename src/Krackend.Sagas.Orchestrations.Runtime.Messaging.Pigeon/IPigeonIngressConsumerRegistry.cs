using Krackend.Sagas.Orchestrations.Runtime.Ingress;

namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

internal interface IPigeonIngressConsumerRegistry
{
    bool TryAttach(
        PigeonIngressConsumerRegistration registration,
        out bool shouldRegisterEndpoint);

    bool TryDetach(
        string connectorId,
        out PigeonIngressConsumerRegistration registration,
        out bool shouldRemoveEndpoint);

    IReadOnlyCollection<IngressConfiguration> GetConfigurations(string endpointKey);

    void RemoveConnector(string connectorId, Action<PigeonIngressConsumerRegistration> removeEndpoint);

    void ForgetConnector(string connectorId);
}
