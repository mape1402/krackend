using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

public interface IReleaseTargetRepository
{
    Task Create(ReleaseTarget assignment, CancellationToken cancellationToken = default);
    Task Update(ReleaseTarget assignment, CancellationToken cancellationToken = default);
    Task AddAttempt(ReleaseAttempt attempt, CancellationToken cancellationToken = default);
    Task<ReleaseTarget> GetById(Id assignmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleaseAttempt>> GetAttempts(Id assignmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleaseTarget>> GetPendingForRuntimeNode(Id runtimeNodeId, CancellationToken cancellationToken = default);
    Task<PagedResult<ReleaseTarget>> GetAll(PagedSettings pagedSettings, CancellationToken cancellationToken = default);
}

