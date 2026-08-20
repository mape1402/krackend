namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control.Decisions
{
    internal sealed record ForwardDecision : IDecision
    {
        public string Kind => "forward";
    }
}
