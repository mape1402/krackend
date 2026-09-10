namespace Krackend.Sagas.Orchestrations.Client.Operations;

using Krackend.Sagas.Orchestrations.Client.Publishing;

/// <summary>
/// Starts and completes orchestration-aware client operations without depending on a pipeline library.
/// </summary>
public interface IOrchestrationOperationClient
{
    /// <summary>
    /// Marks the beginning of a business operation that can participate in an orchestration.
    /// </summary>
    /// <typeparam name="TRequest">CLR request type handled by the operation.</typeparam>
    void Begin<TRequest>();

    /// <summary>
    /// Marks the beginning of a business operation that can participate in an orchestration.
    /// </summary>
    /// <param name="requestType">CLR request type handled by the operation.</param>
    void Begin(Type requestType);

    /// <summary>
    /// Clears the current orchestration operation execution context.
    /// </summary>
    void Close();

    /// <summary>
    /// Reports a successful business operation using transport metadata for orchestration execution data.
    /// </summary>
    /// <typeparam name="TRequest">CLR request type handled by the operation.</typeparam>
    /// <typeparam name="TResponse">CLR response type returned by the operation.</typeparam>
    /// <param name="payload">Business response payload to send to the destination.</param>
    /// <param name="options">Orchestration operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous report operation.</returns>
    Task ReportSuccessAsync<TRequest, TResponse>(
        TResponse payload,
        OrchestrationOperationOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a successful business operation using transport metadata for orchestration execution data.
    /// </summary>
    /// <param name="requestType">CLR request type handled by the operation.</param>
    /// <param name="responseType">CLR response type returned by the operation.</param>
    /// <param name="payload">Business response payload to send to the destination.</param>
    /// <param name="options">Orchestration operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous report operation.</returns>
    Task ReportSuccessAsync(
        Type requestType,
        Type responseType,
        object payload,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a failed business operation using transport metadata for orchestration execution data.
    /// </summary>
    /// <typeparam name="TRequest">CLR request type handled by the operation.</typeparam>
    /// <param name="exception">Exception raised by the operation.</param>
    /// <param name="options">Orchestration operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous report operation.</returns>
    Task ReportFailureAsync<TRequest>(
        Exception exception,
        OrchestrationOperationOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a failed business operation using transport metadata for orchestration execution data.
    /// </summary>
    /// <param name="requestType">CLR request type handled by the operation.</param>
    /// <param name="exception">Exception raised by the operation.</param>
    /// <param name="options">Orchestration operation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous report operation.</returns>
    Task ReportFailureAsync(
        Type requestType,
        Exception exception,
        OrchestrationOperationOptions options,
        CancellationToken cancellationToken = default);
}
