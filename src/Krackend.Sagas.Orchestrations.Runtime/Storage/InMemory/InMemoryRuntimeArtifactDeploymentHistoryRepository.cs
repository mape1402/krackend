namespace Krackend.Sagas.Orchestrations.Runtime.Storage.InMemory;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class InMemoryRuntimeArtifactDeploymentHistoryRepository : IRuntimeArtifactDeploymentHistoryRepository, IRuntimeArtifactHistoryCatalog
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

    public Task<RuntimeArtifactHistoryPage> GetPage(Id artifactId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var records = _store.ArtifactDeploymentHistory.Values.Where(x => x.RuntimeOrchestrationArtifactId == artifactId)
            .OrderByDescending(x => x.AppliedOnUtc).ThenByDescending(x => x.Id.ToString()).ToArray();
        return Task.FromResult(new RuntimeArtifactHistoryPage(records.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), records.LongLength, page, pageSize));
    }
}
