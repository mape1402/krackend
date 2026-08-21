using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists append-only execution timeline transitions.
/// </summary>
public interface IExecutionTransitionRepository
{
    /// <summary>
    /// Creates a transition entry in the execution timeline.
    /// </summary>
    Task Create(ExecutionTransition transition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all transitions recorded for a single orchestration instance.
    /// </summary>
    Task<IReadOnlyCollection<ExecutionTransition>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent transitions for an environment.
    /// </summary>
    Task<IReadOnlyCollection<ExecutionTransition>> GetRecent(
        string environmentKey,
        int take = 250,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets bucketed runtime traffic for an environment since the supplied UTC timestamp.
    /// </summary>
    Task<IReadOnlyCollection<RuntimeTrafficPoint>> GetTraffic(
        string environmentKey,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents bucketed runtime activity used by diagnostics surfaces.
/// </summary>
public sealed record RuntimeTrafficPoint(
    DateTime BucketUtc,
    int Active,
    int Started,
    int Completed,
    int Failed);
