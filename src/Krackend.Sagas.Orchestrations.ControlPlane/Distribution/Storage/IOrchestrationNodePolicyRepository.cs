using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

public interface IOrchestrationNodePolicyRepository
{
    Task Replace(string orchestrationDefinitionId, IReadOnlyCollection<OrchestrationAllowedRuntimeNode> allowedNodes, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Id>> GetAllowedRuntimeNodeIds(string orchestrationDefinitionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<Id>>> GetAllowedRuntimeNodeIdsByOrchestrationIds(
        IReadOnlyCollection<string> orchestrationDefinitionIds,
        CancellationToken cancellationToken = default);
}

