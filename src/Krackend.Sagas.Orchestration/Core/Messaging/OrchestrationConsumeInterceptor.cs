namespace Krackend.Sagas.Orchestration.Core
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;

    /// <summary>
    /// Intercepts message consumption to extract and set orchestration metadata from incoming messages, logging a warning if metadata is missing or extraction fails.
    /// </summary>
    internal sealed class OrchestrationConsumeInterceptor : IConsumeInterceptor
    {
        private readonly IMetadataSetter _metadataSetter;
        private readonly ILogger<OrchestrationConsumeInterceptor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestrationConsumeInterceptor"/> class.
        /// </summary>
        /// <param name="metadataSetter">The metadata setter used to apply extracted metadata.</param>
        /// <param name="logger">The logger used to record warnings if metadata extraction fails.</param>
        public OrchestrationConsumeInterceptor(IMetadataSetter metadataSetter, ILogger<OrchestrationConsumeInterceptor> logger)
        {
            _metadataSetter = metadataSetter ?? throw new ArgumentNullException(nameof(metadataSetter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Attempts to extract orchestration metadata from the consume context and set it on the target object. Logs a warning if extraction fails.
        /// </summary>
        /// <param name="context">The context of the message being consumed.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var metadata = context.GetMetadata<Metadata>(Metadata.MetadataKey);
                _metadataSetter.SetMetadata(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract orchestration metadata from consume context. This may indicate that the message does not contain orchestration metadata.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
