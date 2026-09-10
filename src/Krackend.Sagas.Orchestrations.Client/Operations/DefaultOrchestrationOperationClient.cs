namespace Krackend.Sagas.Orchestrations.Client.Operations;

using Krackend.Sagas.Orchestrations.Client.Publishing;

internal sealed class DefaultOrchestrationOperationClient : IOrchestrationOperationClient
{
    private readonly IOrchestrationOperationExecutionContext _executionContext;
    private readonly IOrchestrationPipelinePublisher _pipelinePublisher;

    public DefaultOrchestrationOperationClient(
        IOrchestrationOperationExecutionContext executionContext,
        IOrchestrationPipelinePublisher pipelinePublisher)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _pipelinePublisher = pipelinePublisher ?? throw new ArgumentNullException(nameof(pipelinePublisher));
    }

    public void Begin(Type requestType)
        => _executionContext.Start(requestType ?? throw new ArgumentNullException(nameof(requestType)));

    public Task ReportSuccessAsync(
        Type requestType,
        Type responseType,
        object payload,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default)
        => _pipelinePublisher.PublishSuccessAsync(
            requestType,
            responseType,
            payload,
            options ?? new OrchestrationOperationOptions(),
            cancellationToken);

    public Task ReportFailureAsync(
        Type requestType,
        Exception exception,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default)
        => _pipelinePublisher.PublishFailureAsync(
            requestType,
            exception,
            options ?? new OrchestrationOperationOptions(),
            cancellationToken);
}
