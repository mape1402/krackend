namespace Krackend.Sagas.Orchestrations.Client.Routing;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Metadata;

internal sealed class OrchestrationTriggerRoute<TRequest, TResponse>
{
    public OrchestrationTriggerRoute(
        Func<TRequest, TResponse, bool> predicate,
        Func<TRequest, TResponse, object> transform,
        OrchestrationReplyAddress address)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Transform = transform ?? throw new ArgumentNullException(nameof(transform));
        Address = address;
    }

    public Func<TRequest, TResponse, bool> Predicate { get; }

    public Func<TRequest, TResponse, object> Transform { get; }

    public OrchestrationReplyAddress Address { get; }
}
