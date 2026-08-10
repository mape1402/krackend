using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class TriggerIntakeAttemptEntity
{
    public Id Id { get; set; }
    public Id TriggerIntakeId { get; set; }
    public int AttemptNumber { get; set; }
    public string ActionType { get; set; }
    public string Outcome { get; set; }
    public DateTime StartedOnUtc { get; set; }
    public DateTime? FinishedOnUtc { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public string MetadataJson { get; set; }
}
