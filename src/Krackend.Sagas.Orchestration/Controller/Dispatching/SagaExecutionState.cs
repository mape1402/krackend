namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Shared;
    using Krackend.Sagas.Orchestration.Controller.Tracking.Models;
    using Krackend.Sagas.Orchestration.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Represents the execution state of a saga, including current, previous, and next stages, as well as orchestration metadata and state transitions.
    /// </summary>
    public class SagaExecutionState
    {
        private readonly Roadmap _roadmap;
        private readonly StateMachine _stateMachine;
        private readonly OrchestrationMetadata _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="SagaExecutionState"/> class using a roadmap and state machine.
        /// </summary>
        /// <param name="roadmap">The roadmap describing the orchestration stages.</param>
        /// <param name="stateMachine">The state machine managing saga state transitions.</param>
        public SagaExecutionState(Roadmap roadmap, StateMachine stateMachine)
        {
            _roadmap = roadmap ?? throw new ArgumentNullException(nameof(roadmap));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));

            OrchestrationKey = roadmap.OrchestrationKey;
            OrchestrationName = roadmap.OrchestrationName;
            SagaState = SagaState.Forward;

            CurrentStage = roadmap.GetFirstStage()?.Id;
            NextStage = roadmap.NextStage(CurrentStage)?.Id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SagaExecutionState"/> class using orchestration metadata and a state machine.
        /// </summary>
        /// <param name="metadata">The orchestration metadata.</param>
        /// <param name="stateMachine">The state machine managing saga state transitions.</param>
        public SagaExecutionState(OrchestrationMetadata metadata, StateMachine stateMachine)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));

            OrchestrationKey = metadata.OrchestrationKey;
            OrchestrationName = metadata.OrchestrationName;
            SagaState = stateMachine.State;

            PreviousStage = metadata.PreviousStage;
            CurrentStage = metadata.CurrentStage;
            NextStage = metadata.NextStage;
        }

        /// <summary>
        /// Gets the unique key for the orchestration instance.
        /// </summary>
        public string OrchestrationKey { get; }

        /// <summary>
        /// Gets the name of the orchestration.
        /// </summary>
        public string OrchestrationName { get; }

        /// <summary>
        /// Gets or sets the unique identifier for the saga instance.
        /// </summary>
        public string SagaId { get; init; }

        /// <summary>
        /// Gets the payload associated with the current stage.
        /// </summary>
        public object Payload { get; private set; }

        /// <summary>
        /// Gets the identifier of the previous stage.
        /// </summary>
        public string PreviousStage { get; private set; }

        /// <summary>
        /// Gets the identifier of the current stage.
        /// </summary>
        public string CurrentStage { get; private set; }

        /// <summary>
        /// Gets the identifier of the next stage.
        /// </summary>
        public string NextStage { get; private set; }

        /// <summary>
        /// Gets the current state of the saga (forward or backward).
        /// </summary>
        public SagaState SagaState { get; private set; }

        /// <summary>
        /// Gets the status of the saga.
        /// </summary>
        public SagaStatus SagaStatus { get; private set; }

        /// <summary>
        /// Gets or sets the service provider for dependency injection.
        /// </summary>
        public IServiceProvider Services { get; init; }

        /// <summary>
        /// Closes the current stage and moves to the next or previous stage based on the operation result.
        /// </summary>
        public void CloseAndMoveStage()
        {
            var logger = Services.GetService<ILogger<SagaExecutionState>>();

            // TODO: mmmm maybe should be a flag like 'IsSuccess', because not only returns one error, returns all attempts
            if (_metadata?.OperationalResults?.HasError == true)
            {
                var attempts = Enumerable.Empty<Attempt>(); // TODO: Get attempts from metadata;
                _stateMachine.CloseCurrentStage(StageStatus.Failed, attempts);
                logger.LogInformation("Saga with id '{0}' has been changed to status '{1}'", SagaId, StageStatus.Failed);
                Backward();
            }
            else
            {
                var attempts = Enumerable.Empty<Attempt>(); // TODO: Get attempts from metadata;
                _stateMachine.CloseCurrentStage(StageStatus.Completed, attempts);
                logger.LogInformation("Saga with id '{0}' has been changed to status '{1}'", SagaId, StageStatus.Completed);
                Forward();
            }      
        }

        /// <summary>
        /// Starts the current stage in the state machine.
        /// </summary>
        public void StartCurrentStage()
        {
            _stateMachine.StartStage(CurrentStage);
        }

        /// <summary>
        /// Sets the payload for the current stage.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        public void SetPayload(object payload)
            => Payload = payload;

        /// <summary>
        /// Moves the execution state forward to the next stage.
        /// </summary>
        public void Forward()
        {
            if (string.IsNullOrWhiteSpace(CurrentStage))
                throw new InvalidOperationException("Current stage is not set.");

            if (string.IsNullOrWhiteSpace(NextStage))
            {
                SagaStatus = SagaStatus.Completed;
                return;
            }

            PreviousStage = CurrentStage;
            CurrentStage = NextStage;
            NextStage = _roadmap.NextStage(CurrentStage)?.Id;
        }

        /// <summary>
        /// Moves the execution state backward to the previous stage and sets the saga state to backward.
        /// </summary>
        public void Backward()
        {
            if (string.IsNullOrWhiteSpace(CurrentStage))
                throw new InvalidOperationException("Current stage is not set.");

            if (string.IsNullOrWhiteSpace(NextStage))
            {
                SagaStatus = SagaStatus.Completed;
                return;
            }

            var previousStage = CurrentStage;

            CurrentStage = PreviousStage;
            PreviousStage = previousStage;
            NextStage = _roadmap.PreviousStage(CurrentStage)?.Id;

            SagaState = SagaState.Backward;
        }

        /// <summary>
        /// Gets the orchestration topic for the current execution state.
        /// </summary>
        /// <returns>The orchestration topic as a string.</returns>
        public string GetOrchestrationTopic()
            => _roadmap?.EventTopic ?? _metadata?.OrchestrationTopic
               ?? throw new InvalidOperationException("Orchestration topic is not set.");

        /// <summary>
        /// Gets the orchestration topic version for the current execution state.
        /// </summary>
        /// <returns>The orchestration topic version as a <see cref="SemanticVersion"/>.</returns>
        public SemanticVersion GetOrchestrationTopicVersion()
            => _roadmap?.EventTopicVersion ?? _metadata?.OrchestrationTopicVersion
               ?? throw new InvalidOperationException("Orchestration topic version is not set.");

        /// <summary>
        /// Gets the underlying state machine for the saga execution.
        /// </summary>
        /// <returns>The <see cref="StateMachine"/> instance.</returns>
        public StateMachine GetStateMachine()
            => _stateMachine;
    }
}
