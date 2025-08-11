namespace Krackend.Sagas.Orchestration.Controller.Tracking
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Tracking.Models;

    /// <summary>
    /// Defines methods for managing state machines for saga orchestration, including creation, retrieval, and persistence.
    /// </summary>
    public interface IStateMachineManager
    {
        /// <summary>
        /// Creates a new state machine for the given roadmap and saga ID.
        /// </summary>
        /// <param name="roadmap">The roadmap describing the orchestration stages.</param>
        /// <param name="sagaId">The unique identifier for the saga instance.</param>
        /// <returns>The created state machine.</returns>
        StateMachine CreateNewStateMachine(Roadmap roadmap, string sagaId);

        /// <summary>
        /// Persists the state machine asynchronously.
        /// </summary>
        /// <param name="stateMachine">The state machine to persist.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task Set(StateMachine stateMachine, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the state machine for the given saga ID asynchronously.
        /// </summary>
        /// <param name="sagaId">The unique identifier for the saga instance.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The state machine associated with the saga ID.</returns>
        Task<StateMachine> Get(string sagaId, CancellationToken cancellationToken = default);
    }
}
