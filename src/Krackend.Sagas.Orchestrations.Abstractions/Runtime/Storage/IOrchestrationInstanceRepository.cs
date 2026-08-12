using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists orchestration instance state.
/// </summary>
public interface IOrchestrationInstanceRepository
{
    Task Create(OrchestrationInstance instance, CancellationToken cancellationToken = default);

    Task Update(OrchestrationInstance instance, CancellationToken cancellationToken = default);

    Task<OrchestrationInstance> GetById(Id instanceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrchestrationInstance>> GetRecent(
        string environmentKey,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<RuntimeInstanceSummary> GetSummary(
        string environmentKey,
        DateTime recentSinceUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RuntimeInstanceSummary(
    int Active,
    int Waiting,
    int CompletedRecent,
    int FailedRecent,
    DateTime RecentSinceUtc);
