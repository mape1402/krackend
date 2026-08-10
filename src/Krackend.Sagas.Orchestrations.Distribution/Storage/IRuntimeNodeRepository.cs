using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage;

public interface IRuntimeNodeRepository
{
    Task Create(RuntimeNode runtimeNode, CancellationToken cancellationToken = default);
    Task Update(RuntimeNode runtimeNode, CancellationToken cancellationToken = default);
    Task SetIsEnabled(Id runtimeNodeId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<RuntimeNode> GetById(Id runtimeNodeId, CancellationToken cancellationToken = default);
    Task<PagedResult<RuntimeNode>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}
