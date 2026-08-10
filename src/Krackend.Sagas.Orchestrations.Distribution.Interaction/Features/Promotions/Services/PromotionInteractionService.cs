using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Enums;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class ReleaseInteractionService : IReleaseInteractionService
{
    private readonly IReleaseRepository _repository;
    private readonly IReleaseTargetRepository _assignmentRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IRuntimeNodeRepository _runtimeNodeRepository;
    private readonly IOrchestrationNodePolicyRepository _policyRepository;

    public ReleaseInteractionService(
        IReleaseRepository repository,
        IReleaseTargetRepository assignmentRepository,
        IArtifactRepository artifactRepository,
        IRuntimeNodeRepository runtimeNodeRepository,
        IOrchestrationNodePolicyRepository policyRepository)
    {
        _repository = repository;
        _assignmentRepository = assignmentRepository;
        _artifactRepository = artifactRepository;
        _runtimeNodeRepository = runtimeNodeRepository;
        _policyRepository = policyRepository;
    }

    public async Task<string> Create(CreateReleaseInput input, CancellationToken cancellationToken = default)
    {
        var artifactId = new Id(Ulid.Parse(input.ArtifactId));
        var artifact = await _artifactRepository.GetById(artifactId, cancellationToken);
        var orchestrationDefinitionId = artifact.OrchestrationDefinitionId;

        var selectedNodeIds = input.RuntimeNodeIds.Select(x => new Id(Ulid.Parse(x))).Distinct().ToArray();
        var allowedNodeIds = await _policyRepository.GetAllowedRuntimeNodeIds(orchestrationDefinitionId, cancellationToken);

        if (allowedNodeIds.Count == 0)
        {
            throw new InvalidOperationException($"No runtime node policy configured for orchestration '{orchestrationDefinitionId}'.");
        }

        var allowedSet = new HashSet<Id>(allowedNodeIds);
        var unauthorizedNode = selectedNodeIds.FirstOrDefault(x => !allowedSet.Contains(x));
        if (unauthorizedNode != default)
        {
            throw new InvalidOperationException($"Runtime node '{unauthorizedNode}' is not allowed for orchestration '{orchestrationDefinitionId}'.");
        }

        var selectedNodes = new Dictionary<Id, RuntimeNode>();
        foreach (var nodeId in selectedNodeIds)
        {
            var node = await _runtimeNodeRepository.GetById(nodeId, cancellationToken);
            if (!node.IsEnabled)
            {
                throw new InvalidOperationException($"Runtime node '{node.Name}' is disabled.");
            }

            selectedNodes[nodeId] = node;
        }

        var release = new Release
        {
            Id = Id.New(),
            ArtifactId = artifactId,
            OrchestrationDefinitionId = orchestrationDefinitionId,
            RequestedBy = string.IsNullOrWhiteSpace(input.RequestedBy) ? "web-ui" : input.RequestedBy.Trim(),
            Strategy = string.IsNullOrWhiteSpace(input.Strategy) ? "Immediate" : input.Strategy.Trim(),
            Status = ReleaseStatus.InProgress, CreatedAtUtc = DateTime.UtcNow
        };
        var targets = selectedNodeIds.Select(id => new ReleasePlanTarget
        {
            Id = Id.New(), ReleaseId = release.Id, RuntimeNodeId = id,
            Status = ReleaseStatus.InProgress, CreatedAtUtc = DateTime.UtcNow, Notes = input.Notes ?? string.Empty
        }).ToArray();

        await _repository.Create(release, targets, cancellationToken);

        foreach (var target in targets)
        {
            var node = selectedNodes[target.RuntimeNodeId];
            var releaseTarget = new ReleaseTarget
            {
                Id = Id.New(), RuntimeNodeId = target.RuntimeNodeId, ArtifactId = release.ArtifactId,
                ReleaseId = release.Id, RolloutGroup = release.Strategy, Status = GetInitialTargetStatus(node.DistributionMode),
                ActivationStatus = ActivationStatus.NotActivated, AssignedAtUtc = DateTime.UtcNow,
                AvailableAtUtc = node.DistributionMode == DistributionMode.Pull ? DateTime.UtcNow : null,
                CorrelationId = release.Id.ToString()
            };

            await _assignmentRepository.Create(releaseTarget, cancellationToken);

        }

        return release.Id.ToString();
    }

    public async Task<InteractionPagedResult<ReleaseModel>> GetAll(InteractionPagedSettings settings, CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAll(new PagedSettings(settings.PageNumber, settings.PageSize, Array.Empty<QueryFilter>(), Array.Empty<QuerySort>()), cancellationToken);
        var rows = new List<ReleaseModel>();
        foreach (var item in result.Rows)
        {
            var targets = await _repository.GetTargets(item.Id, cancellationToken);
            rows.Add(new ReleaseModel
            {
                Id = item.Id.ToString(), ArtifactId = item.ArtifactId.ToString(), RequestedBy = item.RequestedBy,
                OrchestrationDefinitionId = item.OrchestrationDefinitionId,
                Strategy = item.Strategy, Status = item.Status.ToString(), CreatedAtUtc = item.CreatedAtUtc, CompletedAtUtc = item.CompletedAtUtc,
                Targets = targets.Select(t => new ReleasePlanTargetModel
                {
                    Id = t.Id.ToString(), RuntimeNodeId = t.RuntimeNodeId.ToString(), Status = t.Status.ToString(),
                    CreatedAtUtc = t.CreatedAtUtc, CompletedAtUtc = t.CompletedAtUtc, Notes = t.Notes
                }).ToArray()
            });
        }

        return new InteractionPagedResult<ReleaseModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = rows
        };
    }

    private static ReleaseTargetStatus GetInitialTargetStatus(DistributionMode mode)
        => mode == DistributionMode.Pull
            ? ReleaseTargetStatus.AvailableForPull
            : ReleaseTargetStatus.PushScheduled;
}


