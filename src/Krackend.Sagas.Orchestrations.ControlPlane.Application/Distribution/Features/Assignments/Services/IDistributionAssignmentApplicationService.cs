namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IReleaseTargetApplicationService
{
    Task<ApplicationPagedResult<ReleaseTargetModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<ReleaseTargetModel>> Search(int pageNumber, int pageSize, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release target service does not support catalog search.");
    Task<IReadOnlyCollection<ReleaseAttemptModel>> GetAttempts(string assignmentId, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<ReleaseAttemptModel>> SearchAttempts(string assignmentId, int pageNumber, int pageSize, string search, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release target service does not support attempt search.");
    Task<IReadOnlyCollection<ReleaseTargetModel>> GetByRelease(string releaseId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release target service does not support release lookup.");
    Task<IReadOnlyCollection<ReleaseTargetModel>> GetByReleases(IReadOnlyCollection<string> releaseIds, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release target service does not support batch release lookup.");
}

