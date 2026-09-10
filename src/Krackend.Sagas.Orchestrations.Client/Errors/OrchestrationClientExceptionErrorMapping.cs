namespace Krackend.Sagas.Orchestrations.Client.Errors;

/// <summary>
/// Maps a CLR exception type to an orchestration error code.
/// </summary>
public sealed class OrchestrationClientExceptionErrorMapping
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationClientExceptionErrorMapping"/> class.
    /// </summary>
    /// <param name="exceptionType">Exception type matched by the mapping.</param>
    /// <param name="errorCode">Error code understood by the orchestrator definition.</param>
    /// <param name="predicate">Optional predicate that must match the exception.</param>
    /// <param name="isRetryableCandidate">Optional retryability hint reported to the orchestrator.</param>
    public OrchestrationClientExceptionErrorMapping(
        Type exceptionType,
        string errorCode,
        Func<Exception, bool> predicate = null,
        bool? isRetryableCandidate = null)
    {
        ExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
        ErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? throw new ArgumentException("Error code cannot be empty.", nameof(errorCode))
            : errorCode;
        Predicate = predicate;
        IsRetryableCandidate = isRetryableCandidate;
    }

    /// <summary>
    /// Gets the exception type matched by the mapping.
    /// </summary>
    public Type ExceptionType { get; }

    /// <summary>
    /// Gets the orchestration error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the optional predicate that must match the exception.
    /// </summary>
    public Func<Exception, bool> Predicate { get; }

    /// <summary>
    /// Gets the optional retryability hint reported to the orchestrator.
    /// </summary>
    public bool? IsRetryableCandidate { get; }

    /// <summary>
    /// Determines whether this mapping applies to the specified exception.
    /// </summary>
    /// <param name="exception">Exception raised by the business operation.</param>
    /// <returns><see langword="true"/> when the mapping applies.</returns>
    public bool Matches(Exception exception)
        => exception is not null &&
           ExceptionType.IsAssignableFrom(exception.GetType()) &&
           (Predicate is null || Predicate(exception));
}
