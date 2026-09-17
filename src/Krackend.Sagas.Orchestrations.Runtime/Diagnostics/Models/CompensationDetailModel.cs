namespace Krackend.Sagas.Orchestrations.Runtime.Diagnostics;

/// <summary>
/// Represents compensation execution diagnostics for a source task.
/// </summary>
public sealed record CompensationDetailModel(
    string Id,
    string SourceTaskExecutionId,
    string SourceTaskKey,
    string CompensationTaskKey,
    string Status,
    DateTime? StartedOnUtc,
    DateTime? CompletedOnUtc,
    DateTime? FailedOnUtc,
    string RequestPayload,
    string ResponsePayload,
    string ErrorMessage,
    string Metadata);
