using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.Distribution.Storage;

public interface IEnvironmentRepository
{
    Task Create(RuntimeEnvironment environment, CancellationToken cancellationToken = default);
    Task Update(RuntimeEnvironment environment, CancellationToken cancellationToken = default);
    Task SetIsEnabled(Id environmentId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<RuntimeEnvironment> GetById(Id environmentId, CancellationToken cancellationToken = default);
    Task<PagedResult<RuntimeEnvironment>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}

