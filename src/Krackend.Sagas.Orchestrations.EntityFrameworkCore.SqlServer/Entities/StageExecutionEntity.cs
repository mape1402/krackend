using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class StageExecutionEntity
{
    public Id Id { get; set; }
    public Id OrchestrationInstanceId { get; set; }
    public string StageKey { get; set; }
    public int Order { get; set; }
    public StageExecutionStatus Status { get; set; }
    public bool WasSkipped { get; set; }
    public string SkipReason { get; set; }
    public bool? ExecutionConditionResult { get; set; }
    public DateTime? StartedOnUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public DateTime? FailedOnUtc { get; set; }
    public string ErrorSummary { get; set; }
    public int ParallelGroupCount { get; set; }
    public string MetadataJson { get; set; }
}
