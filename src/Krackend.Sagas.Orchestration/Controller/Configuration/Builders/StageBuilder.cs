namespace Krackend.Sagas.Orchestration.Controller.Configuration.Builders
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;

    /// <summary>
    /// Provides an internal implementation for building a stage in saga orchestration.
    /// </summary>
    internal class StageBuilder : IInternalStageBuilder
    {
        private StageConfiguration _configuration = new();

        /// <inheritdoc/>
        public IStageBuilder Set(string id, string name)
        {
            _configuration.Id = id;
            _configuration.Name = name;
            return this;
        }

        /// <inheritdoc/>
        public IStageBuilder Description(string description)
        {
            _configuration.Description = description;
            return this;
        }

        /// <inheritdoc/>
        public IStageBuilder StepForward(Action<IStepBuilder> config)
        {
            var stepBuilder = new StepBuilder();
            config(stepBuilder);

            _configuration.StepForwardBuilder = stepBuilder;

            return this;
        }

        /// <inheritdoc/>
        public IStageBuilder Compensation(Action<IStepBuilder> config)
        {
            var stepBuilder = new StepBuilder();
            config(stepBuilder);

            _configuration.CompensationBuilder = stepBuilder;

            return this;
        }

        /// <inheritdoc/>
        public StageMetadata Build()
            => new StageMetadata
            {
                Id = _configuration.Id,
                Name = _configuration.Name,
                StepForward = _configuration.StepForwardBuilder?.Buid(),
                Compensation = _configuration.CompensationBuilder?.Buid()
            };
    }
}
