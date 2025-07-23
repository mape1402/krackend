namespace Krackend.Sagas.Orchestration.Working
{
    using Spider.Pipelines.Core;

    /// <summary>
    /// Provides extension methods for configuring default forwarding and error handling in Spider pipeline builders.
    /// </summary>
    public static class SpiderExtensions
    {
        /// <summary>
        /// Configures the pipeline builder to use default forwarding and error forwarding for the specified request type.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <param name="builder">The pipeline builder to configure.</param>
        /// <returns>The updated pipeline builder with default forwarding and error handling.</returns>
        public static IPipelineBuilder<TRequest> DefaultForwading<TRequest>(this IPipelineBuilder<TRequest> builder)
        {
            return builder
                        .OnPostProcess(after =>
                        {
                            after
                                .ForwardSuccess()
                                .ForwardError();
                        });
        }

        /// <summary>
        /// Configures the pipeline builder to use default forwarding and error forwarding for the specified request and response types.
        /// </summary>
        /// <typeparam name="TRequest">The type of the operation request.</typeparam>
        /// <typeparam name="TResponse">The type of the operation response.</typeparam>
        /// <param name="builder">The pipeline builder to configure.</param>
        /// <returns>The updated pipeline builder with default forwarding and error handling.</returns>
        public static IPipelineBuilder<TRequest, TResponse> DefaultForwading<TRequest, TResponse>(this IPipelineBuilder<TRequest, TResponse> builder)
        {
            return builder
                        .OnPostProcess(after =>
                        {
                            after
                                .ForwardSuccess()
                                .ForwardError();
                        });
        }
    }
}
