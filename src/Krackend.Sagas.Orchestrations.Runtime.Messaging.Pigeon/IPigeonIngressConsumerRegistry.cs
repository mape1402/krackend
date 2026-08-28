namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon
{
    internal interface IPigeonIngressConsumerRegistry
    {
        bool TryBeginRegistration(string endpointKey);

        void Forget(string endpointKey);
    }
}
