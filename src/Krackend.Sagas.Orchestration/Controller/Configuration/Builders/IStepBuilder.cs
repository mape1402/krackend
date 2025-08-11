namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Provides internal methods for building an orchestration step.
    /// </summary>
    public interface IInternalStepBuilder : IStepBuilder
    {
        /// <summary>
        /// Builds and returns the step metadata.
        /// </summary>
        /// <returns>The metadata of the step.</returns>
        StepMetadata Buid();
    }

    /// <summary>
    /// Provides methods to configure and build an orchestration step.
    /// </summary>
    public interface IStepBuilder
    {
        /// <summary>
        /// Configures the resolve-to destination for the step.
        /// </summary>
        /// <param name="topic">The destination topic.</param>
        /// <returns>The current <see cref="IStepBuilder"/> instance.</returns>
        IStepBuilder ResolveTo(string topic);

        /// <summary>
        /// Configures the resolve-to destination for the step with a specific version.
        /// </summary>
        /// <param name="topic">The destination topic.</param>
        /// <param name="version">The semantic version of the topic.</param>
        /// <returns>The current <see cref="IStepBuilder"/> instance.</returns>
        IStepBuilder ResolveTo(string topic, SemanticVersion version);
    }
}
