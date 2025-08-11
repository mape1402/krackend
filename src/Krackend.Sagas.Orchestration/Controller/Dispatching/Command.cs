namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Represents a command in saga orchestration.
    /// </summary>
    public class Command : ICommand
    {
        /// <summary>
        /// Gets or sets the execution state of the saga associated with this command.
        /// </summary>
        public SagaExecutionState State { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="state">The state associated with the command.</param>
        public Command(SagaExecutionState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
