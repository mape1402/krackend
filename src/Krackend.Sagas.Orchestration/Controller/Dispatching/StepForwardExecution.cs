namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Krackend.Sagas.Orchestration.Controller.Shared;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;

    internal class StepForwardExecution : IStep
    {
        private readonly string _topic;
        private readonly SemanticVersion _version;

        /// <summary>
        /// Initializes a new instance of the <see cref="StepForwardExecution"/> class.
        /// </summary>
        /// <param name="topic">The topic to publish the message to.</param>
        /// <param name="version">The semantic version of the message.</param>
        public StepForwardExecution(string topic, SemanticVersion version)
        {
            _topic = string.IsNullOrWhiteSpace(topic) ? throw new ArgumentNullException(nameof(topic)) : topic;
            _version = version;
        }

        /// <summary>
        /// Processes the step execution asynchronously.
        /// </summary>
        /// <param name="state">The current state of the saga execution.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ProcessAsync(SagaExecutionState state, CancellationToken cancellationToken = default)
        {
            var logger = state.Services.GetService<ILogger<StepForwardExecution>>();
            var producer = state.Services.GetRequiredService<IProducer>();
            await producer.PublishAsync(state.Payload, _topic, _version, cancellationToken);
            logger.LogInformation("Saga with id '{0}' has dispatched to topic '{1}' with version '{2}'", state.SagaId, _topic, _version);
        }
    }
}
