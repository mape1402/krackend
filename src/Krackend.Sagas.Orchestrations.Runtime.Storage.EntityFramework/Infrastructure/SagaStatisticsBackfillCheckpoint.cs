namespace Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;

internal sealed class SagaStatisticsBackfillCheckpoint
{
    public string Id { get; set; }
    public DateTime CompletedOnUtc { get; set; }
}
