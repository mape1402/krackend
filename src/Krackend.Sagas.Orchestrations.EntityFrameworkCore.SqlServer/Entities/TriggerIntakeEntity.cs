using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class TriggerIntakeEntity
{
    public Id Id { get; set; }
    public TriggerType TriggerType { get; set; }
    public string TriggerKey { get; set; }
    public string EnvironmentKey { get; set; }
    public string CorrelationId { get; set; }
    public string IdempotencyKey { get; set; }
    public string SourceMessageId { get; set; }
    public string SourceRequestId { get; set; }
    public string RawPayloadJson { get; set; }
    public string NormalizedPayloadJson { get; set; }
    public TriggerIntakeStatus Status { get; set; }
    public string PersistenceLevel { get; set; }
    public string BufferLocation { get; set; }
    public Id? ResolvedArtifactId { get; set; }
    public Id? PromotedInstanceId { get; set; }
    public DateTime ReceivedOnUtc { get; set; }
    public DateTime? PromotedOnUtc { get; set; }
    public DateTime? ExpiresOnUtc { get; set; }
    public string RejectionReason { get; set; }
    public string FailureReason { get; set; }
}
