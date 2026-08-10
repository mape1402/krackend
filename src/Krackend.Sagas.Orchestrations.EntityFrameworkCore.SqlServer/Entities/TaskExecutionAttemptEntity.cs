using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class TaskExecutionAttemptEntity
{
    public Id Id { get; set; }
    public Id TaskExecutionId { get; set; }
    public int AttemptNumber { get; set; }
    public TaskExecutionStatus Status { get; set; }
    public DateTime? StartedOnUtc { get; set; }
    public DateTime? WaitingSinceUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public DateTime? FailedOnUtc { get; set; }
    public DateTime? TimedOutOnUtc { get; set; }
    public string RequestPayloadJson { get; set; }
    public string ResponsePayloadJson { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public Id? DispatchId { get; set; }
    public string MetadataJson { get; set; }
}
