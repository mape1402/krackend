namespace Krackend.Sagas.Orchestration.Worker.Pipelines
{
    using Microsoft.Extensions.DependencyInjection;
    using Spider.Pipelines.PostProcessing;

    /// <summary>
    /// Provides extension methods for forwarding operations on successful pipeline execution within the orchestration pipeline.
    /// </summary>
    public static class SpiderSuccessExtensions
    {
        /// <summary>
        /// Configures the pipeline to forward the operation request on success using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardSuccess<TRequest>(this IPostProcessConfiguration<TRequest> config)
            => config.ForwardSuccess(null, null);

        /// <summary>
        /// Configures the pipeline to forward the operation request on success using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardSuccess<TRequest>(this IPostProcessConfiguration<TRequest> config, Func<TRequest, object> transformPayload)
            => config.ForwardSuccess(null, transformPayload);

        /// <summary>
        /// Configures the pipeline to forward the operation request on success to a specific topic using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the operation should be forwarded.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardSuccess<TRequest>(this IPostProcessConfiguration<TRequest> config, string topic)
            => config.ForwardSuccess(topic, null);

        /// <summary>
        /// Configures the pipeline to forward the operation request on success to a specific topic using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the operation should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request payload before forwarding.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest> ForwardSuccess<TRequest>(this IPostProcessConfiguration<TRequest> config, string topic, Func<TRequest, object> transformPayload)
        {
            config
                .OnSuccess(async (context, args) =>
                {
                    var successService = context.Services.GetRequiredService<ISuccessService>();
                    await successService.Forward(context, topic, transformPayload);
                });

            return config;
        }

        /// <summary>
        /// Configures the pipeline to forward the operation request and response on success using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardSuccess<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config)
            => config.ForwardSuccess(null, null);

        /// <summary>
        /// Configures the pipeline to forward the operation request and response on success using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="transformPayload">A function to transform the operation request and response payload before forwarding.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardSuccess<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config, Func<TRequest, TResponse, object> transformPayload)
            => config.ForwardSuccess(null, transformPayload);

        /// <summary>
        /// Configures the pipeline to forward the operation request and response on success to a specific topic using the specified context.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the operation should be forwarded.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardSuccess<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config, string topic)
            => config.ForwardSuccess(topic, null);

        /// <summary>
        /// Configures the pipeline to forward the operation request and response on success to a specific topic using the specified context and a payload transformation function.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="config">The post-process configuration to extend.</param>
        /// <param name="topic">The topic to which the operation should be forwarded.</param>
        /// <param name="transformPayload">A function to transform the operation request and response payload before forwarding.</param>
        /// <returns>The updated post-process configuration.</returns>
        public static IPostProcessConfiguration<TRequest, TResponse> ForwardSuccess<TRequest, TResponse>(this IPostProcessConfiguration<TRequest, TResponse> config, string topic, Func<TRequest, TResponse, object> transformPayload)
        {
            config
                .OnSuccess(async (context, args) =>
                {
                    var successService = context.Services.GetRequiredService<ISuccessService>();
                    await successService.Forward(context, topic, transformPayload);
                });

            return config;
        }
    }
}
