namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Dispatching;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Provides an internal implementation for building a step in saga orchestration.
    /// </summary>
    internal class StepBuilder : IInternalStepBuilder
    {
        private readonly StepConfiguration _configuration = new();

        /// <inheritdoc/>
        public IStepBuilder ResolveTo(string topic)
            => ResolveTo(topic, SemanticVersion.Default);

        /// <inheritdoc/>
        public IStepBuilder ResolveTo(string topic, SemanticVersion version)
        {
            _configuration.ResolveTo = topic;
            _configuration.Version = version;
            return this;
        }

        /// <inheritdoc/>
        public StepMetadata Buid()
            => new StepMetadata
            {
                AllowedRetries = _configuration.AllowedRetries,
                ResolveTo = _configuration.ResolveTo,
                Version = _configuration.Version,
                StepExecution = new StepForwardExecution(_configuration.ResolveTo, _configuration.Version) // TODO: Use a factory
            };
    }
}
