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

    void ForgetConnector(string connectorId);
}
