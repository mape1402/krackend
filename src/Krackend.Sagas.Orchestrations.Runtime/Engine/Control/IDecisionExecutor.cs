namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control
{
    internal interface IDecisionExecutor
    {
        Task ExecuteAsync(IDecision decision, CancellationToken cancellationToken = default);
    }
}
