namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    internal interface IDecisionHandler<TDecision>
        where TDecision : IDecision
    {
        Task HandleAsync(TDecision decision, CancellationToken cancellationToken = default);
    }
}
