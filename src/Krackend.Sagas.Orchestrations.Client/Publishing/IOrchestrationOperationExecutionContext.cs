namespace Krackend.Sagas.Orchestrations.Client.Publishing;

internal interface IOrchestrationOperationExecutionContext
{
    void Start(Type requestType);

    DateTime StartedOnUtc { get; }

    Type RequestType { get; }
}
