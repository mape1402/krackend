namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents the command dispatch associated with a task attempt.
/// </summary>
public sealed record DispatchDetailModel(
    string Id,
    string DispatchType,
    string Destination,
    string DispatchStatus,
    string CommandId,
    string CorrelationId,
    DateTime? SentOnUtc,
    DateTime? AcknowledgedOnUtc,
    DateTime? FailedOnUtc,
    string FailureReason,
    string RequestPayload,
    string Metadata,
    string TaskExecutionAttemptId = null);
