namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IArtifactApplicationService
{
    Task<ApplicationPagedResult<ArtifactModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
}

