namespace Krackend.Sagas.Orchestration.Core
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Intercepts publish operations to attach orchestration metadata to outgoing messages. Logs a warning if metadata is not present.
    /// </summary>
    internal sealed class OrchestrationPublishInterceptor : IPublishInterceptor
    {
        private readonly IOrchestrationContext _orchestrationContext;
        private readonly ILogger<OrchestrationPublishInterceptor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestrationPublishInterceptor"/> class.
        /// </summary>
        /// <param name="orchestrationContext">The orchestration context providing metadata for the message.</param>
        /// <param name="logger">The logger used to record warnings if metadata is missing.</param>
        public OrchestrationPublishInterceptor(IOrchestrationContext orchestrationContext, ILogger<OrchestrationPublishInterceptor> logger)
        {
            _orchestrationContext = orchestrationContext ?? throw new ArgumentNullException(nameof(orchestrationContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Intercepts the publish context to add orchestration metadata before the message is published. Logs a warning if metadata is not present in the context.
        /// </summary>
        /// <param name="publishContext">The context of the message being published.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public ValueTask Intercept(PublishContext publishContext, CancellationToken cancellationToken = default)
        {
            if (!_orchestrationContext.HasMetadata())
            {
                _logger.LogWarning("Orchestration metadata is not present in the context. The message will be published without orchestration metadata.");
                return ValueTask.CompletedTask;
            }

            var metadata = _orchestrationContext.GetMetadata();
            publishContext.AddMetadata(Metadata.MetadataKey, metadata);

            return ValueTask.CompletedTask;
        }
    }
}
