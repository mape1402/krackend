namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    /// <summary>
    /// Defines the interface for orchestration configuration of a roadmap.
    /// </summary>
    public interface IOrchestration
    {
        /// <summary>
        /// Configures the roadmap using the provided builder.
        /// </summary>
        /// <param name="builder">The roadmap builder to configure.</param>
        void Configure(IRoadmapBuilder builder);
    }
}
