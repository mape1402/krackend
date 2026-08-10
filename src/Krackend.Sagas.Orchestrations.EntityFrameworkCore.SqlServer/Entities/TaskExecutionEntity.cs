using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class TaskExecutionEntity
{
    public Id Id { get; set; }
    public Id OrchestrationInstanceId { get; set; }
    public Id StageExecutionId { get; set; }
    public string TaskKey { get; set; }
    public TaskKind TaskKind { get; set; }
    public TaskExecutionMode ExecutionMode { get; set; }
    public Id? ParallelGroupId { get; set; }
    public TaskExecutionStatus Status { get; set; }
    public bool WasSkipped { get; set; }
    public string SkipReason { get; set; }
    public bool? ExecutionConditionResult { get; set; }
    public OnErrorPolicy OnErrorPolicy { get; set; }
    public bool AwaitResponse { get; set; }
    public DateTime? StartedOnUtc { get; set; }
    public DateTime? WaitingSinceUtc { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public DateTime? FailedOnUtc { get; set; }
    public DateTime? TimedOutOnUtc { get; set; }
    public int LastAttemptNumber { get; set; }
    public string OutputVariablesPayloadJson { get; set; }
    public string CorrelationId { get; set; }
    public string MetadataJson { get; set; }
}
