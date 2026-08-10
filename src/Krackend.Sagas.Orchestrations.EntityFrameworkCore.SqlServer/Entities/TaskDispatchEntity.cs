using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class TaskDispatchEntity
{
    public Id Id { get; set; }
    public Id TaskExecutionAttemptId { get; set; }
    public string DispatchType { get; set; }
    public string Destination { get; set; }
    public string RequestPayloadJson { get; set; }
    public string DispatchStatus { get; set; }
    public string CommandId { get; set; }
    public string CorrelationId { get; set; }
    public DateTime? SentOnUtc { get; set; }
    public DateTime? AcknowledgedOnUtc { get; set; }
    public DateTime? FailedOnUtc { get; set; }
    public string FailureReason { get; set; }
    public string MetadataJson { get; set; }
}
