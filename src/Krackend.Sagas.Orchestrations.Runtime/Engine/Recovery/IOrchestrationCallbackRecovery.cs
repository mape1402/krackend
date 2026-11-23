namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Recovery;

/// <summary>Recovers accepted callback receipts whose durable action handoff was interrupted.</summary>
public interface IOrchestrationCallbackRecovery
{
    /// <summary>Reconciles a bounded, fair page of callback receipts.</summary>
    Task<int> RecoverAsync(int take, CancellationToken cancellationToken = default);
}
