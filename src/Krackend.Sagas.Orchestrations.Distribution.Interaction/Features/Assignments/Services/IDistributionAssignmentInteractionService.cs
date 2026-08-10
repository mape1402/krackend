namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IReleaseTargetInteractionService
{
    Task<InteractionPagedResult<ReleaseTargetModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ReleaseAttemptModel>> GetAttempts(string assignmentId, CancellationToken cancellationToken = default);
}

