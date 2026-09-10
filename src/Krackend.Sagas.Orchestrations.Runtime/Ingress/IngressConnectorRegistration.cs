namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

internal sealed record IngressConnectorRegistration(string ConnectorId, IngressTransport IngressTransport);
