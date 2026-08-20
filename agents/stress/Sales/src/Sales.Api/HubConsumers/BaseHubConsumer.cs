using Pelican.Mediator;
using Pigeon.Messaging.Consuming.Dispatching;
using Spider.Pipelines.Core;
using TurtlePath.ExceptionHandling.Consumers;

namespace Sales.Api.HubConsumers
{
    /// <summary>
    /// Provides a base hub consumer with access to mediator and spider services.
    /// </summary>
    public abstract class BaseHubConsumer : HubConsumer
    {
        private IMediator _mediator;
        private IConsumerExceptionBoundary _consumerExceptionBoundary;
        private ISpider _spider;

        /// <summary>
        /// Gets the mediator instance from the current context.
        /// </summary>
        public IMediator Mediator => _mediator ??= Context.Services.GetRequiredService<IMediator>();

        /// <summary>
        /// Gets the consumer exception boundary instance from the current context.
        /// </summary>
        public IConsumerExceptionBoundary ConsumerExceptionBoundary =>
            _consumerExceptionBoundary ??= Context.Services.GetRequiredService<IConsumerExceptionBoundary>();

        /// <summary>
        /// Gets the spider instance from the current context.
        /// </summary>
        public ISpider Spider => _spider ??= Context.Services.GetRequiredService<ISpider>();
    }
}
