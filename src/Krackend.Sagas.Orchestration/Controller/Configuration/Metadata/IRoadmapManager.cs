namespace Krackend.Sagas.Orchestration.Controller.Configuration.Metadata
{
    using Krackend.Sagas.Orchestration.Controller.Shared;

    /// <summary>
    /// Defines methods for managing orchestration roadmaps, including retrieval, addition, removal, and updates.
    /// </summary>
    public interface IRoadmapManager
    {
        /// <summary>
        /// Gets the roadmap associated with the specified orchestration key.
        /// </summary>
        /// <param name="orchestrationKey">The unique key identifying the orchestration.</param>
        /// <returns>The <see cref="Roadmap"/> associated with the given orchestration key, or null if not found.</returns>
        Roadmap Get(string orchestrationKey);

        /// <summary>
        /// Gets the roadmap for the specified topic.
        /// </summary>
        /// <param name="topic">The topic to retrieve the roadmap for.</param>
        /// <returns>The roadmap associated with the topic.</returns>
        Roadmap GetRoadmap(string topic);

        /// <summary>
        /// Adds a new roadmap to the manager.
        /// </summary>
        /// <param name="roadmap">The roadmap to add.</param>
        /// <returns>The updated roadmap manager.</returns>
        IRoadmapManager AddRoadmap(Roadmap roadmap);

        /// <summary>
        /// Removes the roadmap with the specified orchestration key.
        /// </summary>
        /// <param name="orchestrationKey">The orchestration key of the roadmap to remove.</param>
        /// <returns>The updated roadmap manager.</returns>
        IRoadmapManager RemoveRoadmap(string orchestrationKey);

        /// <summary>
        /// Marks the roadmap with the specified orchestration key as inactive.
        /// </summary>
        /// <param name="orchestrationKey">The orchestration key of the roadmap to inactivate.</param>
        /// <returns>The updated roadmap manager.</returns>
        IRoadmapManager InactiveRoadmap(string orchestrationKey);

        /// <summary>
        /// Updates the roadmap for the specified orchestrator key.
        /// </summary>
        /// <param name="orchestratorKey">The orchestrator key to update.</param>
        /// <param name="roadmap">The new roadmap to set.</param>
        /// <returns>The updated roadmap manager.</returns>
        IRoadmapManager UpdateRoadmap(string orchestratorKey, Roadmap roadmap);

        /// <summary>
        /// Gets the step execution for a given orchestration key, stage ID, and direction.
        /// </summary>
        /// <param name="orchestrationKey">The orchestration key.</param>
        /// <param name="stageId">The stage ID.</param>
        /// <param name="direction">The direction (forward or backward).</param>
        /// <returns>The step execution for the specified parameters.</returns>
        IStep GetStepExecution(string orchestrationKey, string stageId, SagaState direction);
    }
}
