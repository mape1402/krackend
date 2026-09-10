namespace Krackend.Sagas.Orchestrations.Client.Errors;

/// <summary>
/// Represents the orchestration error-code resolution for a client-side exception.
/// </summary>
/// <param name="ErrorCode">Error code understood by the orchestrator definition.</param>
/// <param name="IsRetryableCandidate">Optional hint indicating whether the exception can be considered retryable.</param>
public sealed record OrchestrationExceptionErrorCodeResolution(
    string ErrorCode,
    bool? IsRetryableCandidate = null);
