namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    /// <summary>
    /// Defines methods for building and starting orchestrators for saga orchestration.
    /// </summary>
    public interface IOrchestratorBuilder
    {
        /// <summary>
        /// Starts the orchestrator with the provided orchestrations.
        /// </summary>
        /// <param name="orchestrations">The orchestrations to start.</param>
        void Start(IEnumerable<IOrchestration> orchestrations);

        /// <summary>
        /// Adds a single orchestration to the orchestrator.
        /// </summary>
        /// <param name="orchestration">The orchestration to add.</param>
        void Add(IOrchestration orchestration);
    }
}
