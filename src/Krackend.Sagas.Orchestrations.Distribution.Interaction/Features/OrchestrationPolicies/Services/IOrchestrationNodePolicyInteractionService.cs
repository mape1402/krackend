namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public interface IOrchestrationNodePolicyInteractionService
{
    Task<IReadOnlyCollection<OrchestrationProjectionModel>> GetOrchestrations(CancellationToken cancellationToken = default);
    Task<OrchestrationNodePolicyModel> Get(string orchestrationDefinitionId, CancellationToken cancellationToken = default);
    Task Replace(ReplaceOrchestrationNodePolicyInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetByOrchestrationIds(
        IReadOnlyCollection<string> orchestrationDefinitionIds,
        CancellationToken cancellationToken = default);
}
