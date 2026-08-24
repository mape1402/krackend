namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IRuntimeNodeApplicationService
{
    Task<string> Upsert(UpsertRuntimeNodeInput input, CancellationToken cancellationToken = default);
    Task SetEnabled(string runtimeNodeId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<RuntimeNodeModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default);
}
