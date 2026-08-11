namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Default payload factory for orchestration output.
/// </summary>
public sealed class OrchestrationOutputPayloadFactory : IOrchestrationOutputPayloadFactory
{
    /// <inheritdoc/>
    public object CreateSuccessPayload<TRequest, TResponse>(
        TRequest request,
        TResponse response,
        Func<TRequest, TResponse, object> transformPayload = null)
        => transformPayload is null ? response : transformPayload(request, response);

    /// <inheritdoc/>
    public object CreateSuccessPayload<TRequest>(
        TRequest request,
        Func<TRequest, object> transformPayload = null)
        => transformPayload is null ? request : transformPayload(request);

    /// <inheritdoc/>
    public object CreateFailurePayload<TRequest>(TRequest request, Exception exception)
    {
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));

        return new OrchestrationFailurePayload
        {
            Request = request,
            ErrorType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message
        };
    }
}
