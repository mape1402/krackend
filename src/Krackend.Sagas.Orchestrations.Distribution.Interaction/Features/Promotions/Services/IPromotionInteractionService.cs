namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IReleaseInteractionService
{
    Task<string> Create(CreateReleaseInput input, CancellationToken cancellationToken = default);
    Task<InteractionPagedResult<ReleaseModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default);
}

