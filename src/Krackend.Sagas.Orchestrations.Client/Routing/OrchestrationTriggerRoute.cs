namespace Krackend.Sagas.Orchestrations.Client.Routing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class OrchestrationTriggerRoute<TRequest>
{
    public OrchestrationTriggerRoute(
        Func<TRequest, bool> predicate,
        Func<TRequest, object> transform,
        OrchestrationReplyAddress address)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Transform = transform ?? throw new ArgumentNullException(nameof(transform));
        Address = address;
    }

    public Func<TRequest, bool> Predicate { get; }

    public Func<TRequest, object> Transform { get; }

    public OrchestrationReplyAddress Address { get; }
}
