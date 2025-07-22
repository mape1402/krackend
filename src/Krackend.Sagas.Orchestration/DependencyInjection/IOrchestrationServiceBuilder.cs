namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Represents a builder for configuring orchestration services and accessing the underlying service collection.
    /// </summary>
    public interface IOrchestrationServiceBuilder
    {
        /// <summary>
        /// Gets the <see cref="IServiceCollection"/> used to register orchestration services.
        /// </summary>
        IServiceCollection Services { get; }
    }
}
