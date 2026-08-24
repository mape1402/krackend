namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IRuntimeEnvironmentApplicationService
{
    Task<string> Upsert(UpsertRuntimeEnvironmentInput input, CancellationToken cancellationToken = default);
    Task SetEnabled(string environmentId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<RuntimeEnvironmentModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
}

