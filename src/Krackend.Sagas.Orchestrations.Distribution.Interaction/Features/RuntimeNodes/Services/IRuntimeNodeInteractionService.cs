namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IRuntimeNodeInteractionService
{
    Task<string> Upsert(UpsertRuntimeNodeInput input, CancellationToken cancellationToken = default);
    Task SetEnabled(string runtimeNodeId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<InteractionPagedResult<RuntimeNodeModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default);
}
