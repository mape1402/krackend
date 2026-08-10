using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage;

public interface IOrchestrationProjectionRepository
{
    Task Upsert(OrchestrationProjection orchestration, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OrchestrationProjection>> GetAll(CancellationToken cancellationToken = default);
    Task<bool> Exists(string orchestrationId, CancellationToken cancellationToken = default);
}


