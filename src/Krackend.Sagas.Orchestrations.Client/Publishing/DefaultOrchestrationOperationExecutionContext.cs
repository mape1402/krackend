namespace Krackend.Sagas.Orchestrations.Client.Publishing;

internal sealed class DefaultOrchestrationOperationExecutionContext : IOrchestrationOperationExecutionContext
{
    private readonly AsyncLocal<ExecutionContextState> _state = new();

    public DateTime StartedOnUtc => _state.Value?.StartedOnUtc ?? default;

    public Type RequestType => _state.Value?.RequestType;

    public void Start(Type requestType)
        => _state.Value = new ExecutionContextState(requestType, DateTime.UtcNow);

    public void Clear()
        => _state.Value = null;

    private sealed record ExecutionContextState(Type RequestType, DateTime StartedOnUtc);
}
