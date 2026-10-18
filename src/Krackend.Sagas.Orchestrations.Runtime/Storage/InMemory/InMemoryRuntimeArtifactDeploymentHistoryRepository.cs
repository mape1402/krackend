namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class InMemoryRuntimeArtifactDeploymentHistoryRepository : IRuntimeArtifactDeploymentHistoryRepository
{
    private readonly InMemoryRuntimeStore _store;

    public InMemoryRuntimeArtifactDeploymentHistoryRepository(InMemoryRuntimeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task Append(RuntimeArtifactDeploymentRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _store.ArtifactDeploymentHistory[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>> GetAll(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>>(
            _store.ArtifactDeploymentHistory.Values
                .OrderByDescending(x => x.AppliedOnUtc)
                .ThenByDescending(x => x.Id.ToString())
                .ToArray());
}
