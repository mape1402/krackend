namespace Krackend.Sagas.Orchestration.Controller.Tracking.Models
{
    using Krackend.Sagas.Orchestration.Controller.Shared;

    /// <summary>
    /// Represents the state machine for a saga instance, including its stages and status.
    /// </summary>
    public class StateMachine
    {
        /// <summary>
        /// Gets or sets the orchestration key.
        /// </summary>
        public string OrchestrationKey { get; init; }

        /// <summary>
        /// Gets or sets the unique identifier for the saga instance.
        /// </summary>
        public string SagaId { get; init; }

        /// <summary>
        /// Gets or sets the start time of the saga.
        /// </summary>
        public DateTimeOffset StartedOn { get; init; }

        /// <summary>
        /// Gets or sets the last updated time of the saga.
        /// </summary>
        public DateTimeOffset? LastUpdatedOn { get; init; }

        /// <summary>
        /// Gets or sets the completion time of the saga.
        /// </summary>
        public DateTimeOffset? CompletedOn { get; init; }

        /// <summary>
        /// Gets or sets the collection of stages in the saga.
        /// </summary>
        public IEnumerable<Stage> Stages { get; init; }
        
        /// <summary>
        /// Gets or sets the current state direction of the saga.
        /// </summary>
        public SagaState State { get; init; }

        /// <summary>
        /// Gets or sets the current status of the saga.
        /// </summary>
        public SagaStatus Status { get; init; }

        /// <summary>
        /// Starts the specified stage by its identifier.
        /// </summary>
        /// <param name="stageId">The identifier of the stage to start.</param>
        public void StartStage(string stageId)
        {
            var stage = Stages.FirstOrDefault(x => x.Id == stageId);

            if (stage == null)
                throw new InvalidOperationException($"Stage with id '{stageId}' doesn't exist.");

            stage.Status = StageStatus.InProgress;
            stage.StepForward.StartedOn = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Closes the current stage with the specified status and attempts.
        /// </summary>
        /// <param name="status">The final status of the stage.</param>
        /// <param name="attempts">The collection of attempts for the stage execution.</param>
        public void CloseCurrentStage(StageStatus status, IEnumerable<Attempt> attempts)
        {
            var currentStage = Stages.FirstOrDefault(x => x.Status == StageStatus.InProgress);
            currentStage.Status = status;

            var execution = State == SagaState.Forward ? currentStage.StepForward : currentStage.Compensation;
            execution.Attempts = attempts;
            execution.CompletedOn = DateTimeOffset.UtcNow;
        }
    }
}
