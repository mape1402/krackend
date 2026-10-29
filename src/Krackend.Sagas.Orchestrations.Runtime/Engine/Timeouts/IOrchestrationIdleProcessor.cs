namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Timeouts;

/// <summary>Promotes elapsed reconciliation windows from Idle to DeadLettered.</summary>
public interface IOrchestrationIdleProcessor
{
    Task<int> ProcessExpiredAsync(DateTime utcNow, int take = 100, CancellationToken cancellationToken = default);
}
