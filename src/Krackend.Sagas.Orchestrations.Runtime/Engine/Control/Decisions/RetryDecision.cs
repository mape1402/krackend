namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record RetryDecision : IDecision
    {
        public string Kind => "retry";
    }
}
