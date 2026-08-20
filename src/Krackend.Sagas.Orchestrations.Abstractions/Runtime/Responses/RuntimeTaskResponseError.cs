namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Responses;

/// <summary>
/// Describes a failed service task execution.
/// </summary>
public sealed class RuntimeTaskResponseError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the exception type.
    /// </summary>
    public string ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets the failure reason supplied by the pipeline.
    /// </summary>
    public string Reason { get; set; }
}
