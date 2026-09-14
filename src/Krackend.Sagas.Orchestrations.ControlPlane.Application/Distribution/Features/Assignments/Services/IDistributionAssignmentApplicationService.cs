namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IReleaseTargetApplicationService
{
    Task<ApplicationPagedResult<ReleaseTargetModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleaseAttemptModel>> GetAttempts(string assignmentId, CancellationToken cancellationToken = default);
}

