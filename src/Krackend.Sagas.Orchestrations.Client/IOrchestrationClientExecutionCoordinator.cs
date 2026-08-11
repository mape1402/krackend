using Krackend.Sagas.Orchestrations.Client.Abstractions;

namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Coordinates orchestration client work around a business execution.
/// </summary>
public interface IOrchestrationClientExecutionCoordinator
{
    /// <summary>
    /// Starts an orchestration-aware execution scope.
    /// </summary>
    OrchestrationExecutionContext Begin(OrchestrationOutputDescriptor output = null);

    /// <summary>
    /// Publishes successful output for a request/response execution.
    /// </summary>
    Task<OrchestrationPublishResult> PublishSuccess<TRequest, TResponse>(
        OrchestrationExecutionContext context,
        TRequest request,
        TResponse response,
        Func<TRequest, TResponse, object> transformPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes successful output for a request-only execution.
    /// </summary>
    Task<OrchestrationPublishResult> PublishSuccess<TRequest>(
        OrchestrationExecutionContext context,
        TRequest request,
        Func<TRequest, object> transformPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes failed output for a request execution.
    /// </summary>
    Task<OrchestrationPublishResult> PublishFailure<TRequest>(
        OrchestrationExecutionContext context,
        TRequest request,
        Exception exception,
        CancellationToken cancellationToken = default);
}
