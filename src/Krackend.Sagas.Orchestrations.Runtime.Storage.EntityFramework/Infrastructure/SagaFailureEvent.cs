using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class SagaFailureEvent
{
    public Id Id { get; set; }
    public Id OrchestrationInstanceId { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string TransitionType { get; set; }
}

internal sealed class SagaFailureBackfillCheckpoint
{
    public string Id { get; set; }
    public DateTime BeforeUtc { get; set; }
    public DateTime CursorUtc { get; set; }
    public int CursorOffset { get; set; }
    public bool Completed { get; set; }
    public string ConcurrencyToken { get; set; }
}
