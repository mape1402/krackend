namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Tracking;
    using Krackend.Sagas.Orchestration.Core;

    /// <summary>
    /// Provides an implementation for managing the execution state of a saga.
    /// </summary>
    internal class StateManager : IStateManager
    {
        private readonly IOrchestrationContext _orchestrationContext;
        private readonly IRoadmapManager _roadmapManager;
        private readonly IStateMachineManager _stateMachineManager;
        private readonly ISagaIdGenerator _sagaIdGenerator;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateManager"/> class.
        /// </summary>
        /// <param name="orchestrationContext">The orchestration context.</param>
        /// <param name="roadmapManager">The roadmap manager.</param>
        /// <param name="stateMachineManager">The state machine manager.</param>
        /// <param name="sagaIdGenerator">The saga ID generator.</param>
        /// <param name="serviceProvider">The service provider for dependency injection.</param>
        public StateManager(IOrchestrationContext orchestrationContext, IRoadmapManager roadmapManager, IStateMachineManager stateMachineManager, 
            ISagaIdGenerator sagaIdGenerator, IServiceProvider serviceProvider)
        {
            _orchestrationContext = orchestrationContext ?? throw new ArgumentNullException(nameof(orchestrationContext));
            _roadmapManager = roadmapManager ?? throw new ArgumentNullException(nameof(roadmapManager));
            _stateMachineManager = stateMachineManager ?? throw new ArgumentNullException(nameof(stateMachineManager));
            _sagaIdGenerator = sagaIdGenerator ?? throw new ArgumentNullException(nameof(sagaIdGenerator));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Creates a new saga execution state for the specified topic.
        /// </summary>
        /// <param name="topic">The topic for which to create the state.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation and containing the saga execution state.</returns>
        public async Task<SagaExecutionState> CreateState(string topic, CancellationToken cancellationToken = default)
        {
            var roadmap = _roadmapManager.GetRoadmap(topic);

            if (roadmap == null)
                throw new InvalidOperationException($"There isn't an orchestration-roadmap linked to topic '{topic}'.");

            var sagaid = await _sagaIdGenerator.CreateNew(cancellationToken);
            var stateMachine = _stateMachineManager.CreateNewStateMachine(roadmap, sagaid);

            var state = new SagaExecutionState(roadmap, stateMachine)
            {
                SagaId = sagaid,
                Services = _serviceProvider
            };

            stateMachine.StartStage(state.CurrentStage);
            await _stateMachineManager.Set(stateMachine, cancellationToken);

            return state;
        }

        /// <summary>
        /// Gets the current saga execution state.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation and containing the saga execution state.</returns>
        public async Task<SagaExecutionState> GetCurrentState(CancellationToken cancellationToken = default)
        {
            if(!_orchestrationContext.HasMetadata())
                throw new InvalidOperationException("No current state available. Ensure that orchestration metadata is set.");

            var metadata = _orchestrationContext.GetMetadata();
            var stateMachine = await _stateMachineManager.Get(metadata.SagaId, cancellationToken);

            var roadmap = _roadmapManager.Get(metadata.OrchestrationKey);

            if (roadmap == null)
                throw new InvalidOperationException($"There isn't an orchestration-roadmap linked to key '{metadata.OrchestrationKey}'.");

            return new SagaExecutionState(roadmap, metadata, stateMachine)
            {
                SagaId = metadata.SagaId,
                Services = _serviceProvider
            };
        }
    }
}
