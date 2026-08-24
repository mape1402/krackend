namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IReleaseApplicationService
{
    Task<string> Create(CreateReleaseInput input, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<ReleaseModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
}

