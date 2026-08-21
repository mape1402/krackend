namespace Krackend.Sagas.Orchestrations.Client.Publishing;

internal interface IOrchestrationPipelinePublisher
{
    Task PublishSuccessAsync(Type requestType, Type responseType, object payload, OrchestrationOperationOptions options, CancellationToken cancellationToken = default);

    Task PublishFailureAsync(Type requestType, Exception exception, OrchestrationOperationOptions options, CancellationToken cancellationToken = default);
}
