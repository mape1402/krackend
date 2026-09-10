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
    public OrchestrationClientExceptionErrorMapping(Type exceptionType, string errorCode)
    {
        ExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
        ErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? throw new ArgumentException("Error code cannot be empty.", nameof(errorCode))
            : errorCode;
    }

    /// <summary>
    /// Gets the exception type matched by the mapping.
    /// </summary>
    public Type ExceptionType { get; }

    /// <summary>
    /// Gets the orchestration error code.
    /// </summary>
    public string ErrorCode { get; }
}
