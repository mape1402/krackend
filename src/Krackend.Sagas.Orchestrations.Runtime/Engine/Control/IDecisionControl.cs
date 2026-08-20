namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    public interface IDecisionControl
    {
        Task<IReadOnlyCollection<IDecision>> DecideAsync(DecisionRequest request, CancellationToken cancellationToken);
    }
}
