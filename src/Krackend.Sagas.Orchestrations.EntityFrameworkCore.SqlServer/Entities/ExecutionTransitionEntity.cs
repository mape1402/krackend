using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.EntityFrameworkCore.SqlServer.Entities;

public sealed class ExecutionTransitionEntity
{
    public Id Id { get; set; }
    public Id OrchestrationInstanceId { get; set; }
    public Id? StageExecutionId { get; set; }
    public Id? TaskExecutionId { get; set; }
    public Id? TaskExecutionAttemptId { get; set; }
    public string TransitionType { get; set; }
    public string FromStatus { get; set; }
    public string ToStatus { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Message { get; set; }
    public string PayloadJson { get; set; }
    public string ProducedBy { get; set; }
}
