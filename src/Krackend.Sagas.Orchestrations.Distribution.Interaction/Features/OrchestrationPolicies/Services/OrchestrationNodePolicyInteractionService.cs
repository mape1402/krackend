using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class OrchestrationNodePolicyInteractionService : IOrchestrationNodePolicyInteractionService
{
    private readonly IOrchestrationNodePolicyRepository _repository;
    private readonly IOrchestrationProjectionRepository _orchestrationRepository;

    public OrchestrationNodePolicyInteractionService(
        IOrchestrationNodePolicyRepository repository,
        IOrchestrationProjectionRepository orchestrationRepository)
    {
        _repository = repository;
        _orchestrationRepository = orchestrationRepository;
    }

    public async Task<IReadOnlyCollection<OrchestrationProjectionModel>> GetOrchestrations(CancellationToken cancellationToken = default)
    {
        var rows = await _orchestrationRepository.GetAll(cancellationToken);
        return rows.Select(x => new OrchestrationProjectionModel
        {
            Id = x.Id,
            Key = x.Key,
            Name = x.Name,
            IsActive = x.IsActive
        }).ToArray();
    }

    public async Task<OrchestrationNodePolicyModel> Get(string orchestrationDefinitionId, CancellationToken cancellationToken = default)
    {
        var nodeIds = await _repository.GetAllowedRuntimeNodeIds(orchestrationDefinitionId, cancellationToken);
        return new OrchestrationNodePolicyModel
        {
            OrchestrationDefinitionId = orchestrationDefinitionId,
            RuntimeNodeIds = nodeIds.Select(x => x.ToString()).ToArray()
        };
    }

    public async Task Replace(ReplaceOrchestrationNodePolicyInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.OrchestrationDefinitionId))
        {
            throw new InvalidOperationException("OrchestrationDefinitionId is required.");
        }
        var exists = await _orchestrationRepository.Exists(input.OrchestrationDefinitionId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException("Orchestration is not available in distribution mirror yet.");
        }

        var nodes = (input.RuntimeNodeIds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Select(x => new OrchestrationAllowedRuntimeNode
            {
                Id = Id.New(),
                OrchestrationDefinitionId = input.OrchestrationDefinitionId,
                RuntimeNodeId = new Id(Ulid.Parse(x)),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = string.IsNullOrWhiteSpace(input.UpdatedBy) ? "web-ui" : input.UpdatedBy.Trim()
            })
            .ToArray();

        await _repository.Replace(input.OrchestrationDefinitionId, nodes, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetByOrchestrationIds(
        IReadOnlyCollection<string> orchestrationDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetAllowedRuntimeNodeIdsByOrchestrationIds(orchestrationDefinitionIds, cancellationToken);
        return rows.ToDictionary(
            x => x.Key,
            x => (IReadOnlyCollection<string>)x.Value.Select(id => id.ToString()).ToArray(),
            StringComparer.Ordinal);
    }
}

