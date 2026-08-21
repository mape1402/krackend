namespace Krackend.Sagas.Orchestrations.Runtime.WebUI.Diagnostics;

/// <summary>
/// Represents a task execution attempt with request, response, timing and dispatch diagnostics.
/// </summary>
public sealed record TaskAttemptDetailModel(
    string Id,
    int AttemptNumber,
    string Status,
    DateTime? StartedOnUtc,
    DateTime? WaitingSinceUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    DateTime? TimedOutOnUtc,
    string RequestPayload,
    string ResponsePayload,
    string ErrorCode,
    string ErrorMessage,
    DispatchDetailModel Dispatch,
    string Metadata,
    string TaskExecutionId = null,
    string DispatchId = null);
