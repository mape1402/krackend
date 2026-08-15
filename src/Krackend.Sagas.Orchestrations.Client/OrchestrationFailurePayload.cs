namespace Krackend.Sagas.Orchestrations.Client;

/// <summary>
/// Default failure payload sent back to orchestration output.
/// </summary>
public sealed class OrchestrationFailurePayload
{
    /// <summary>
    /// Gets whether the orchestrated task succeeded.
    /// </summary>
    public bool Succeeded { get; init; } = false;

    /// <summary>
    /// Gets the failure status.
    /// </summary>
    public string Status { get; init; } = "Failed";

    /// <summary>
    /// Gets the original request.
    /// </summary>
    public required object Request { get; init; }

    /// <summary>
    /// Gets the exception type.
    /// </summary>
    public required string ErrorType { get; init; }

    /// <summary>
    /// Gets the exception message.
    /// </summary>
    public required string Message { get; init; }
}
