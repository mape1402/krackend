namespace Krackend.Sagas.Orchestration.Working.Pipelines
{
    using Spider.Pipelines.Core;

    /// <summary>
    /// Defines methods for forwarding operation errors within an orchestration pipeline, with optional payload transformation and topic selection.
    /// </summary>
    public interface IFailureService
    {
        /// <summary>
        /// Forwards an operation error using the specified context, topic, and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="context">The context containing the operation request.</param>
        /// <param name="topic">The topic to which the error should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding the error.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask ForwardError<TRequest>(IReadOnlyContext<TRequest> context, string topic, Func<TRequest, object> transformPayload);
    }
}
