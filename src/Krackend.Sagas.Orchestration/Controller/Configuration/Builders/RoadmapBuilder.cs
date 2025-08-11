namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Provides an internal implementation for building a roadmap for orchestration.
    /// </summary>
    internal class RoadmapBuilder : IInternalRoadmapBuilder
    {
        private readonly RoadmapConfiguration _configuration = new();

        /// <inheritdoc/>
        public IRoadmapBuilder Set(string key, string name)
        {
            _configuration.OrchestrationKey = key;
            _configuration.OrchestrationName = name;

            return this;
        }

        /// <inheritdoc/>
        public IRoadmapBuilder Description(string description)
        {
            _configuration.Description = description;
            return this;
        }

        /// <inheritdoc/>
        public IRoadmapBuilder SubscribeTo(string topic)
            => SubscribeTo(topic, SemanticVersion.Default);

        /// <inheritdoc/>
        public IRoadmapBuilder SubscribeTo(string topic, SemanticVersion version)
        {
            _configuration.EventTopic = topic;
            _configuration.EventTopicVersion = version;

            return this;
        }

        /// <inheritdoc/>
        public IRoadmapBuilder ListenTo(string topic)
            => ListenTo(topic, SemanticVersion.Default);

        /// <inheritdoc/>
        public IRoadmapBuilder ListenTo(string topic, SemanticVersion version)
        {
            _configuration.OrchestrationTopic = topic;
            _configuration.OrchestrationTopicVersion = version;

            return this;
        }

        /// <inheritdoc/>
        public IRoadmapBuilder AsActive()
        {
            _configuration.IsActive = true;
            return this;
        }

        /// <inheritdoc/>
        public IRoadmapBuilder AsInactive()
        {
            _configuration.IsActive &= false;
            return this;
        }

        /// <inheritdoc/>
        public IRoadmapBuilder Next(Action<IStageBuilder> config)
        {
            var stageBuilder = new StageBuilder();
            config(stageBuilder);

            _configuration.StageBuilders.Add(stageBuilder);

            return this;
        }

        /// <inheritdoc/>
        public Roadmap Build()
            => new Roadmap
            {
                OrchestrationKey = _configuration.OrchestrationKey,
                OrchestrationName = _configuration.OrchestrationName,
                Description = _configuration.Description,
                EventTopic = _configuration.EventTopic,
                EventTopicVersion = _configuration.EventTopicVersion,
                OrchestrationTopic = _configuration.OrchestrationTopic,
                OrchestrationTopicVersion = _configuration.OrchestrationTopicVersion,
                IsActive = _configuration.IsActive,
                Stages = _configuration.StageBuilders.Select(x => x.Build())
            };
    }
}
