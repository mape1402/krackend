namespace Krackend.Sagas.Orchestrations.Runtime.Ingress;

/// <summary>Reads the current routable candidates for one messaging endpoint.</summary>
public interface ISelectiveMessagingIngressAccessor
{
    /// <summary>Reads matching candidates without enumerating unrelated artifact routes.</summary>
    Task<IReadOnlyCollection<IngressConfiguration>> ReadMatchingAsync(string topic, string version,
        CancellationToken cancellationToken = default);
}
