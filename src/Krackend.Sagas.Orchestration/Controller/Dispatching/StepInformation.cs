namespace Krackend.Sagas.Orchestration.Controller.Dispatching
{
    using Krackend.Sagas.Orchestration.Controller.Shared;

    /// <summary>
    /// Defines methods for accessing step information in saga orchestration.
    /// </summary>
    public class StepInformation
    {
        /// <summary>
        /// Gets the resolve-to destination for the step.
        /// </summary>
        public string ResolveTo { get; init; }

        /// <summary>
        /// Gets the number of allowed retries for the step.
        /// </summary>
        public int AllowedRetries { get; init; }

        /// <summary>
        /// Gets the step execution instance.
        /// </summary>
        public IStep Execution { get; init; }
    }
}
