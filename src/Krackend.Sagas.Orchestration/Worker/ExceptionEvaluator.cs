namespace Krackend.Sagas.Orchestration.Worker
{
    using Krackend.Sagas.Orchestration.Core;

    /// <summary>
    /// Provides an implementation of <see cref="IExceptionEvaluator"/> that extracts error metadata from exceptions using a classification handler.
    /// </summary>
    internal sealed class ExceptionEvaluator : IExceptionEvaluator
    {
        private readonly ErrorCodeClasificator _errorCodeClasificator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionEvaluator"/> class.
        /// </summary>
        /// <param name="errorCodeClasificator">The handler used to classify exception types and provide error codes.</param>
        public ExceptionEvaluator(ErrorCodeClasificator errorCodeClasificator)
        {
            _errorCodeClasificator = errorCodeClasificator ?? throw new ArgumentNullException(nameof(errorCodeClasificator));
        }

        /// <inheritdoc/>
        public ErrorMetadata ExtractMetadata(Exception exception)
        {
            return new ErrorMetadata
            {
                Code = _errorCodeClasificator(exception.GetType()),
                Message = exception.Message,
                StackTrace = exception.ToString()
            };
        }
    }
}
