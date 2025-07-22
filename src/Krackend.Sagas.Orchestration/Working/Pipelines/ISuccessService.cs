namespace Krackend.Sagas.Orchestration.Working.Pipelines
{
    using Spider.Pipelines.Core;

    /// <summary>
    /// Defines methods for forwarding operations within an orchestration pipeline, with optional payload transformation and topic selection.
    /// </summary>
    public interface ISuccessService
    {
        /// <summary>
        /// Forwards an operation request to a specific topic using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="context">The context containing the operation request.</param>
        /// <param name="topic">The topic to which the operation should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask Forward<TRequest>(IReadOnlyContext<TRequest> context, string topic, Func<TRequest, object> transformPayload);

        /// <summary>
        /// Forwards an operation request and response to a specific topic using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="context">The context containing the operation request.</param>
        /// <param name="topic">The topic to which the operation should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request and response payload before forwarding.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask Forward<TRequest, TResponse>(IReadOnlyContext<TRequest, TResponse> context, string topic, Func<TRequest, TResponse, object> transformPayload);
    }
}
