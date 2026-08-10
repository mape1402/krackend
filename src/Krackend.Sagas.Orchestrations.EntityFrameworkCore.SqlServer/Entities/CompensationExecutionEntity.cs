using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class CompensationExecutionEntity
{
    public Id Id { get; set; }
    public Id OrchestrationInstanceId { get; set; }
    public Id SourceTaskExecutionId { get; set; }
    public string CompensationTaskKey { get; set; }
    public string Status { get; set; }
    public DateTime? StartedOnUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public DateTime? FailedOnUtc { get; set; }
    public string RequestPayloadJson { get; set; }
    public string ResponsePayloadJson { get; set; }
    public string ErrorMessage { get; set; }
    public string MetadataJson { get; set; }
}
