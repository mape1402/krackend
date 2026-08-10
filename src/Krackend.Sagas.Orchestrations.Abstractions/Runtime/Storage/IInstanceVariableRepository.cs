using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Abstractions.Runtime;

namespace Krackend.Sagas.Orchestrations.Abstractions.Runtime.Storage;

/// <summary>
/// Persists variables attached to an orchestration instance.
/// </summary>
public interface IInstanceVariableRepository
{
    Task Upsert(InstanceVariable variable, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InstanceVariable>> GetByInstanceId(
        Id instanceId,
        CancellationToken cancellationToken = default);
}
