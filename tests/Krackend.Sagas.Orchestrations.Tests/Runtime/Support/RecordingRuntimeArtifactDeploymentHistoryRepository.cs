namespace Krackend.Sagas.Orchestrations.Tests.Runtime.Support;

using Krackend.Sagas.Orchestrations.Abstractions.Runtime;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

internal sealed class RecordingRuntimeArtifactDeploymentHistoryRepository : IRuntimeArtifactDeploymentHistoryRepository
{
    public List<RuntimeArtifactDeploymentRecord> Records { get; } = [];

    public Task Append(RuntimeArtifactDeploymentRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>> GetAll(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<RuntimeArtifactDeploymentRecord>>(Records.ToArray());
}
