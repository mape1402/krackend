namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IOrchestrationNodePolicyApplicationService
{
    Task<IReadOnlyCollection<OrchestrationPolicyDefinitionModel>> GetOrchestrations(CancellationToken cancellationToken = default);
    Task<OrchestrationNodePolicyModel> Get(string orchestrationDefinitionId, CancellationToken cancellationToken = default);
    Task Replace(ReplaceOrchestrationNodePolicyInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetByOrchestrationIds(
        IReadOnlyCollection<string> orchestrationDefinitionIds,
        CancellationToken cancellationToken = default);
}
