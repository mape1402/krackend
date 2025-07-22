namespace Krackend.Sagas.Orchestration.Working
{
    using Krackend.Sagas.Orchestration.Working.Pipelines;
    using Microsoft.Extensions.DependencyInjection;
    using Spider.Pipelines.PostProcessing;

    /// <summary>
    /// Provides extension methods for forwarding operation errors on pipeline failure within the orchestration pipeline.
    /// </summary>
    public static class SpiderFailureExtensions
    {
        /// <summary>
        /// Configures the pipeline to forward the operation error on failure using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardError<TRequest>(this IPostProcessConfiguration<TRequest> config)
            => config.ForwardError(null, null);

        /// <summary>
        /// Configures the pipeline to forward the operation error on failure using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding the error.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardError<TRequest>(this IPostProcessConfiguration<TRequest> config, Func<TRequest, object> transformPayload)
            => config.ForwardError(null, transformPayload);

        /// <summary>
        /// Configures the pipeline to forward the operation error on failure to a specific topic using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the error should be forwarded.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardError<TRequest>(this IPostProcessConfiguration<TRequest> config, string topic)
            => config.ForwardError(topic, null);

        /// <summary>
        /// Configures the pipeline to forward the operation error on failure to a specific topic using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the error should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding the error.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardError<TRequest>(this IPostProcessConfiguration<TRequest> config, string topic, Func<TRequest, object> transformPayload)
        {
            config
                .OnFailure(async (context, args) =>
                {
                    var failureService = context.Services.GetRequiredService<IFailureService>();
                    await failureService.ForwardError(context, topic, transformPayload);
                });

            return config;
        }

        /// <summary>
        /// Configures the pipeline to forward the operation error and response on failure using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardError<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config)
            => config.ForwardError(null, null);

        /// <summary>
        /// Configures the pipeline to forward the operation error and response on failure using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="transformPayload">A function to transform the operation request and response payload before forwarding the error.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardError<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config, Func<TRequest, object> transformPayload)
            => config.ForwardError(null, transformPayload);

        /// <summary>
        /// Configures the pipeline to forward the operation error and response on failure to a specific topic using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the error should be forwarded.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardError<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config, string topic)
            => config.ForwardError(topic, null);

        /// <summary>
        /// Configures the pipeline to forward the operation error and response on failure to a specific topic using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the error should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding the error.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardError<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config, string topic, Func<TRequest, object> transformPayload)
        {
            config
                .OnFailure(async (context, args) =>
                {
                    var failureService = context.Services.GetRequiredService<IFailureService>();
                    await failureService.ForwardError(context, topic, transformPayload);
                });

            return config;
        }
    }
}
