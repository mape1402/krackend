namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IArtifactInteractionService
{
    Task<InteractionPagedResult<ArtifactModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default);
}

