namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    /// <summary>
    /// Represents a command in saga orchestration.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Gets the execution state of the saga associated with this command.
        /// </summary>
        SagaExecutionState State { get; }
    }
}
