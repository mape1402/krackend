namespace Krackend.Sagas.Orchestration.Controller.Tracking
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Shared;
    using Krackend.Sagas.Orchestration.Controller.Tracking.Models;
    using System.Collections.Concurrent;

    /// <summary>
    /// Provides an internal implementation for managing the lifecycle of state machines in saga orchestration.
    /// </summary>
    internal class StateMachineManager : IStateMachineManager
    {
        private readonly ConcurrentDictionary<string, StateMachine> _stateMachines = new();

        /// <summary>
        /// Creates a new state machine for the given roadmap and saga ID.
        /// </summary>
        /// <param name="roadmap">The roadmap describing the orchestration stages.</param>
        /// <param name="sagaId">The unique identifier for the saga instance.</param>
        /// <returns>The created state machine.</returns>
        public StateMachine CreateNewStateMachine(Roadmap roadmap, string sagaId)
        {
            var state = new StateMachine
            {
                SagaId = sagaId,
                OrchestrationKey = roadmap.OrchestrationKey,
                StartedOn = DateTime.UtcNow,
                State = SagaState.Forward,
                Status = SagaStatus.InProgress,
                Stages = roadmap.Stages.Select(x => new Stage
                {
                    Id = x.Id,
                    Name = x.Name,
                    Status = StageStatus.Pending,
                    Compensation = new StageExecution
                    {
                        ResolveTo = x.Compensation?.ResolveTo
                    },
                    StepForward = new StageExecution
                    {
                        ResolveTo = x.StepForward?.ResolveTo
                    }
                }).ToArray()
            };

            return state;
        }

        /// <summary>
        /// Retrieves the state machine for the given saga ID asynchronously.
        /// </summary>
        /// <param name="sagaId">The unique identifier for the saga instance.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The state machine associated with the saga ID.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the state machine does not exist.</exception>
        public Task<StateMachine> Get(string sagaId, CancellationToken cancellationToken = default)
        {
            if (_stateMachines.TryGetValue(sagaId, out var stateMachine))
                return Task.FromResult(stateMachine);

            throw new InvalidOperationException($"The state machine for saga with id '{sagaId}' doesn't exist.");
        }

        /// <summary>
        /// Persists or updates the provided state machine asynchronously.
        /// </summary>
        /// <param name="stateMachine">The state machine to save.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task Set(StateMachine stateMachine, CancellationToken cancellationToken = default)
        {
            _stateMachines.AddOrUpdate(stateMachine.OrchestrationKey, stateMachine, (key, value) => value);
            return Task.CompletedTask;
        }
    }
}
