using Krackend.Sagas.Orchestrations.Abstractions.Primitives;

namespace Krackend.Sagas.Orchestrations.Runtime.Engine.Coordination;

/// <summary>Activates already-persisted work after the producing mutation releases its lease.</summary>
internal interface IOrchestrationPostLeaseWork
{
    bool HasPendingWork(Id instanceId);
    bool IsWithinMutation(Id instanceId);

    /// <summary>
    /// Registers independent durable work that can run concurrently after the instance lease is released.
    /// Every activation receives a fresh dependency-injection scope so scoped storage is never shared
    /// across concurrent operations.
    /// </summary>
    Task ExecuteIsolatedAfterLeaseReleaseAsync(
        Id instanceId,
        Func<IServiceProvider, CancellationToken, Task> activation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a continuation that must run after all independent activations have completed.
    /// </summary>
    Task ExecuteAfterLeaseReleaseAsync(Id instanceId, Func<CancellationToken, Task> activation,
        CancellationToken cancellationToken = default);
}
