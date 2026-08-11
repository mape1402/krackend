namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Creates payloads to publish after business execution.
/// </summary>
public interface IOrchestrationOutputPayloadFactory
{
    /// <summary>
    /// Creates a success payload for a request/response execution.
    /// </summary>
    object CreateSuccessPayload<TRequest, TResponse>(
        TRequest request,
        TResponse response,
        Func<TRequest, TResponse, object> transformPayload = null);

    /// <summary>
    /// Creates a success payload for a request-only execution.
    /// </summary>
    object CreateSuccessPayload<TRequest>(
        TRequest request,
        Func<TRequest, object> transformPayload = null);

    /// <summary>
    /// Creates a failure payload.
    /// </summary>
    object CreateFailurePayload<TRequest>(TRequest request, Exception exception);
}
