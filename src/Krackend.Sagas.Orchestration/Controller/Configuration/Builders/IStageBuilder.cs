namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;

    /// <summary>
    /// Provides internal methods for building an orchestration stage.
    /// </summary>
    public interface IInternalStageBuilder : IStageBuilder
    {
        /// <summary>
        /// Builds and returns the stage metadata.
        /// </summary>
        /// <returns>The metadata of the stage.</returns>
        StageMetadata Build();
    }

    /// <summary>
    /// Provides methods to configure and build an orchestration stage.
    /// </summary>
    public interface IStageBuilder
    {
        /// <summary>
        /// Sets the identifier and name of the stage.
        /// </summary>
        /// <param name="id">The stage identifier.</param>
        /// <param name="name">The stage name.</param>
        /// <returns>The current <see cref="IStageBuilder"/> instance.</returns>
        IStageBuilder Set(string id, string name);

        /// <summary>
        /// Sets the description of the stage.
        /// </summary>
        /// <param name="description">The stage description.</param>
        /// <returns>The current <see cref="IStageBuilder"/> instance.</returns>
        IStageBuilder Description(string description);

        /// <summary>
        /// Configures the forward step of the stage.
        /// </summary>
        /// <param name="config">The configuration action for the forward step.</param>
        /// <returns>The current <see cref="IStageBuilder"/> instance.</returns>
        IStageBuilder StepForward(Action<IStepBuilder> config);

        /// <summary>
        /// Configures the compensation step of the stage.
        /// </summary>
        /// <param name="config">The configuration action for the compensation step.</param>
        /// <returns>The current <see cref="IStageBuilder"/> instance.</returns>
        IStageBuilder Compensation(Action<IStepBuilder> config);
    }
}
