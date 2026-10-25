namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IReleaseApplicationService
{
    Task<string> Create(CreateReleaseInput input, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<ReleaseModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<ReleaseModel>> Search(ApplicationPagedSettings settings, string orchestrationId, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release service does not support catalog search.");
    Task<ReleaseModel> GetById(string id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured release service does not support detail lookup.");
}

