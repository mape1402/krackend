using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class OrchestrationNodePolicyApplicationService : IOrchestrationNodePolicyApplicationService
{
    private readonly IOrchestrationNodePolicyRepository _repository;
    private readonly IOrchestrationDefinitionRepository _orchestrationRepository;

    public OrchestrationNodePolicyApplicationService(
        IOrchestrationNodePolicyRepository repository,
        IOrchestrationDefinitionRepository orchestrationRepository)
    {
        _repository = repository;
        _orchestrationRepository = orchestrationRepository;
    }

    public async Task<IReadOnlyCollection<OrchestrationPolicyDefinitionModel>> GetOrchestrations(CancellationToken cancellationToken = default)
    {
        var rows = await _orchestrationRepository.GetAll(
            new PagedSettings(
                1,
                int.MaxValue,
                Array.Empty<QueryFilter>(),
                Array.Empty<QuerySort>()),
            cancellationToken);

        return rows.Rows.Select(x => new OrchestrationPolicyDefinitionModel
        {
            Id = x.Id.ToString(),
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
        try
        {
            await _orchestrationRepository.GetById(new Id(Ulid.Parse(input.OrchestrationDefinitionId)), cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            throw new InvalidOperationException("Orchestration definition was not found.");
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

