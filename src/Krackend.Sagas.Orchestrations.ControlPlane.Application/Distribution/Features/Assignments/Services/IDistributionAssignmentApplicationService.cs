namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IReleaseTargetApplicationService
{
    Task<ApplicationPagedResult<ReleaseTargetModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleaseAttemptModel>> GetAttempts(string assignmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleaseTargetModel>> GetByRelease(string releaseId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release target service does not support release lookup.");
}

