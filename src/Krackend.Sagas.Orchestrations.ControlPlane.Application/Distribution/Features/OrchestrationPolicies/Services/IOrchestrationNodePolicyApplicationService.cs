namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public interface IOrchestrationNodePolicyApplicationService
{
    Task<IReadOnlyCollection<OrchestrationPolicyDefinitionModel>> GetOrchestrations(CancellationToken cancellationToken = default);
    Task<ApplicationPagedResult<OrchestrationPolicyDefinitionModel>> SearchOrchestrations(ApplicationPagedSettings settings, string search, string sort, bool descending, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured policy service does not support orchestration search.");
    Task<OrchestrationPolicyDefinitionModel> GetOrchestration(string id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The configured policy service does not support orchestration lookup.");
    Task<OrchestrationNodePolicyModel> Get(string orchestrationDefinitionId, CancellationToken cancellationToken = default);
    Task Replace(ReplaceOrchestrationNodePolicyInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetByOrchestrationIds(
        IReadOnlyCollection<string> orchestrationDefinitionIds,
        CancellationToken cancellationToken = default);
}
