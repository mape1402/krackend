namespace Krackend.Sagas.Orchestrations.Runtime.Ingress.Messaging;

/// <summary>
/// Selects the runtime ingress configurations that should process an incoming messaging envelope.
/// </summary>
public interface IMessagingIngressConfigurationSelector
{
    /// <summary>
    /// Selects active messaging ingress configurations matching the incoming topic and version.
    /// </summary>
    /// <param name="configurations">Candidate ingress configurations.</param>
    /// <param name="topic">Incoming messaging topic.</param>
    /// <param name="version">Incoming messaging version.</param>
    /// <returns>Ingress configurations that should receive the incoming message.</returns>
    IReadOnlyCollection<IngressConfiguration> SelectMatching(
        IReadOnlyCollection<IngressConfiguration> configurations,
        string topic,
        string version);
}
