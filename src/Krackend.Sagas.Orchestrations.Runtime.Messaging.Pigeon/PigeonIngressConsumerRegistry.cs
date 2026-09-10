namespace Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;

internal sealed class PigeonIngressConsumerRegistry : IPigeonIngressConsumerRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PigeonIngressConsumerRegistration> _connectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _endpointConnectors = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAttach(
        PigeonIngressConsumerRegistration registration,
        out bool shouldRegisterEndpoint)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_sync)
        {
            if (_connectors.ContainsKey(registration.ConnectorId))
            {
                shouldRegisterEndpoint = false;
                return false;
            }

            if (!_endpointConnectors.TryGetValue(registration.EndpointKey, out var connectorIds))
            {
                connectorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _endpointConnectors.Add(registration.EndpointKey, connectorIds);
            }

            connectorIds.Add(registration.ConnectorId);
            _connectors.Add(registration.ConnectorId, registration);
            shouldRegisterEndpoint = connectorIds.Count == 1;
            return true;
        }
    }

    public bool TryDetach(
        string connectorId,
        out PigeonIngressConsumerRegistration registration,
        out bool shouldRemoveEndpoint)
    {
        lock (_sync)
        {
            if (!_connectors.Remove(connectorId, out registration))
            {
                shouldRemoveEndpoint = false;
                registration = null!;
                return false;
            }

            shouldRemoveEndpoint = false;
            if (_endpointConnectors.TryGetValue(registration.EndpointKey, out var connectorIds))
            {
                connectorIds.Remove(connectorId);
                shouldRemoveEndpoint = connectorIds.Count == 0;
                if (shouldRemoveEndpoint)
                {
                    _endpointConnectors.Remove(registration.EndpointKey);
                }
            }

            return true;
        }
    }

    public void ForgetConnector(string connectorId)
        => TryDetach(connectorId, out _, out _);
}
