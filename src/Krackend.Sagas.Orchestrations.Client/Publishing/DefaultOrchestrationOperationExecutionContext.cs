namespace Krackend.Sagas.Orchestrations.Client.Publishing;

internal sealed class DefaultOrchestrationOperationExecutionContext : IOrchestrationOperationExecutionContext
{
    public DateTime StartedOnUtc { get; private set; }

    public Type RequestType { get; private set; }

    public void Start(Type requestType)
    {
        RequestType = requestType;
        StartedOnUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        RequestType = null;
        StartedOnUtc = default;
    }
}
