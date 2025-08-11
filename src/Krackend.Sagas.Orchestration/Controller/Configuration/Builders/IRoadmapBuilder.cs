namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Provides methods to build an internal roadmap for saga orchestration.
    /// </summary>
    public interface IInternalRoadmapBuilder : IRoadmapBuilder
    {
        /// <summary>
        /// Builds and returns the configured roadmap.
        /// </summary>
        /// <returns>The constructed <see cref="Roadmap"/>.</returns>
        Roadmap Build();
    }

    /// <summary>
    /// Provides methods to configure and build a roadmap for orchestration.
    /// </summary>
    public interface IRoadmapBuilder
    {
        /// <summary>
        /// Sets the key and name for the roadmap.
        /// </summary>
        /// <param name="key">The unique key for the roadmap.</param>
        /// <param name="name">The name of the roadmap.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder Set(string key, string name);

        /// <summary>
        /// Sets the description for the roadmap.
        /// </summary>
        /// <param name="description">The description text.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder Description(string description);

        /// <summary>
        /// Subscribes the roadmap to the specified topic.
        /// </summary>
        /// <param name="topic">The topic to subscribe to.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder SubscribeTo(string topic);

        /// <summary>
        /// Subscribes the roadmap to the specified topic and version.
        /// </summary>
        /// <param name="topic">The topic to subscribe to.</param>
        /// <param name="version">The semantic version of the topic.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder SubscribeTo(string topic, SemanticVersion version);

        /// <summary>
        /// Configures the roadmap to listen to the specified topic.
        /// </summary>
        /// <param name="topic">The topic to listen to.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder ListenTo(string topic);

        /// <summary>
        /// Configures the roadmap to listen to the specified topic and version.
        /// </summary>
        /// <param name="topic">The topic to listen to.</param>
        /// <param name="version">The semantic version of the topic.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder ListenTo(string topic, SemanticVersion version);

        /// <summary>
        /// Marks the roadmap as active.
        /// </summary>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder AsActive();

        /// <summary>
        /// Marks the roadmap as inactive.
        /// </summary>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder AsInactive();

        /// <summary>
        /// Configures the next stage in the roadmap.
        /// </summary>
        /// <param name="config">The configuration action for the next stage.</param>
        /// <returns>The current <see cref="IRoadmapBuilder"/> instance.</returns>
        IRoadmapBuilder Next(Action<IStageBuilder> config);
    }
}
