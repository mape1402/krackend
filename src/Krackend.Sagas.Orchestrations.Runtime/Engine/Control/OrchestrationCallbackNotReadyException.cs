namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Control;

internal sealed class OrchestrationCallbackNotReadyException : InvalidOperationException
{
    public OrchestrationCallbackNotReadyException(string message)
        : base(message)
    {
    }
}
