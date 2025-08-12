namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Dispatching;
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;

    /// <summary>
    /// Provides an implementation of <see cref="IOrchestratorBuilder"/> for configuring and starting orchestrations.
    /// </summary>
    internal class OrchestratorBuilder : IOrchestratorBuilder
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IRoadmapManager _roadmapManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrchestratorBuilder"/> class.
        /// </summary>
        /// <param name="consumingConfigurator">The consuming configurator for message consumers.</param>
        /// <param name="roadmapManager">The roadmap manager for orchestration roadmaps.</param>
        public OrchestratorBuilder(IConsumingConfigurator consumingConfigurator, IRoadmapManager roadmapManager)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _roadmapManager = roadmapManager ?? throw new ArgumentNullException(nameof(roadmapManager));
        }

        /// <inheritdoc />
        public void Start(IEnumerable<IOrchestration> orchestrations)
        {
            foreach (var orchestration in orchestrations)
                Add(orchestration);
        }

        /// <inheritdoc />
        public void Add(IOrchestration orchestration)
        {
            if (orchestration == null)
                throw new ArgumentNullException(nameof(orchestration));

            var roadmapBuilder = new RoadmapBuilder();
            orchestration.Configure(roadmapBuilder);

            var roadmap = roadmapBuilder.Build();
            _roadmapManager.AddRoadmap(roadmap);

            _consumingConfigurator
                .AddConsumer<object>(roadmap.EventTopic, roadmap.EventTopicVersion, static async (context, message) =>
                {
                    var dispatcher = context.Services.GetRequiredService<IDispatcher>();
                    await dispatcher.StartAsync(context.Topic, context.MessageVersion, message, context.CancellationToken);
                })
                .AddConsumer<object>(roadmap.OrchestrationTopic, roadmap.OrchestrationTopicVersion, static async (context, message) =>
                {
                    var dispatcher = context.Services.GetRequiredService<IDispatcher>();
                    await dispatcher.ForwardAsync(message, context.CancellationToken);
                });
        }
    }
}
