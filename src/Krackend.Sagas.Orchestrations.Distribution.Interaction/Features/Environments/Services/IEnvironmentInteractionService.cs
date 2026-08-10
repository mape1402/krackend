namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IRuntimeEnvironmentInteractionService
{
    Task<string> Upsert(UpsertRuntimeEnvironmentInput input, CancellationToken cancellationToken = default);
    Task SetEnabled(string environmentId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<InteractionPagedResult<RuntimeEnvironmentModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default);
}

