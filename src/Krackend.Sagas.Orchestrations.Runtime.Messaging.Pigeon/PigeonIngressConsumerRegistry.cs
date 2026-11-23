using Krackend.Sagas.Orchestrations.Runtime.Ingress;

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
            if (_connectors.TryGetValue(registration.ConnectorId, out var existing))
            {
                if (!string.Equals(existing.EndpointKey, registration.EndpointKey, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Connector '{registration.ConnectorId}' cannot move between Pigeon endpoints without being disconnected first.");

                // Refreshing an artifact can change disposition or ownership while
                // retaining the physical endpoint and connector id. Replace the
                // logical route without touching the broker consumer.
                _connectors[registration.ConnectorId] = registration;
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

    public IReadOnlyCollection<IngressConfiguration> GetConfigurations(string endpointKey)
    {
        lock (_sync)
        {
            if (!_endpointConnectors.TryGetValue(endpointKey, out var connectorIds)) return [];
            return connectorIds
                .Select(id => _connectors.TryGetValue(id, out var registration)
                    ? registration.Configuration
                    : null)
                .Where(configuration => configuration is not null)
                .Cast<IngressConfiguration>()
                .ToArray();
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

    public void RemoveConnector(string connectorId, Action<PigeonIngressConsumerRegistration> removeEndpoint)
    {
        ArgumentNullException.ThrowIfNull(removeEndpoint);
        lock (_sync)
        {
            if (!_connectors.TryGetValue(connectorId, out var registration)) return;
            if (!_endpointConnectors.TryGetValue(registration.EndpointKey, out var connectorIds) || !connectorIds.Contains(connectorId))
                throw new InvalidOperationException("The Pigeon connector registry is inconsistent.");
            if (connectorIds.Count == 1)
                removeEndpoint(registration);
            _connectors.Remove(connectorId);
            connectorIds.Remove(connectorId);
            if (connectorIds.Count == 0) _endpointConnectors.Remove(registration.EndpointKey);
        }
    }
}
