namespace Krackend.Sagas.Orchestration.Core
{
    /// <summary>
    /// Provides an implementation of <see cref="IOrchestrationContext"/> and <see cref="IMetadataSetter"/> for managing orchestration metadata and error state.
    /// </summary>
    internal sealed class OrchestrationContext : IOrchestrationContext, IMetadataSetter
    {
        private OrchestrationMetadata _metadata;
        private ErrorMetadata _errorMetadata;

        /// <inheritdoc />
        public ErrorMetadata GetErrorMetadata()
            => _errorMetadata;

        /// <inheritdoc />
        public OrchestrationMetadata GetMetadata()
            => _metadata;

        /// <inheritdoc />
        public bool HasError()
            => _errorMetadata != null;

        /// <inheritdoc />
        public bool HasMetadata()
            => _metadata != null;

        /// <inheritdoc />
        public void SetAsFailure(ErrorMetadata errorMetadata)
        {
            if(errorMetadata == null)
                throw new ArgumentNullException(nameof(errorMetadata), "Error metadata cannot be null.");

            _errorMetadata = errorMetadata;

            if (_metadata.OperationalResults == null)
                _metadata.OperationalResults = new();

            _metadata.OperationalResults.HasError = true;
        }

        /// <inheritdoc />
        public void SetAsSuccess()
        {
            if (_metadata.OperationalResults == null)
                _metadata.OperationalResults = new();

            _metadata.OperationalResults.HasError = false;
        }

        /// <inheritdoc />
        public void SetMetadata(OrchestrationMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata), "Metadata cannot be null.");

            _metadata = metadata;
        }
    }
}
