using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

public sealed class ReleaseApplicationService : IReleaseApplicationService
{
    private readonly IReleaseRepository _repository;
    private readonly IReleaseTargetRepository _assignmentRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IRuntimeNodeRepository _runtimeNodeRepository;
    private readonly IOrchestrationNodePolicyRepository _policyRepository;

    public ReleaseApplicationService(
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
            if (node.IsDeleted || node.Status != RuntimeNodeStatus.Enabled)
            {
                throw new InvalidOperationException($"Runtime node '{node.Name}' is not enabled.");
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
                AvailableAtUtc = node.DistributionMode == DistributionMode.RuntimeFetchesFromDesign ? DateTime.UtcNow : null,
                CorrelationId = release.Id.ToString()
            };

            await _assignmentRepository.Create(releaseTarget, cancellationToken);

        }

        return release.Id.ToString();
    }

    public async Task<ApplicationPagedResult<ReleaseModel>> GetAll(ApplicationPagedSettings settings, CancellationToken cancellationToken = default)
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

        return new ApplicationPagedResult<ReleaseModel>
        {
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalRows = (int)result.TotalRows,
            TotalPages = result.TotalPages,
            Rows = rows
        };
    }

    private static ReleaseTargetStatus GetInitialTargetStatus(DistributionMode mode)
        => mode == DistributionMode.RuntimeFetchesFromDesign
            ? ReleaseTargetStatus.AvailableForPull
            : ReleaseTargetStatus.PushScheduled;
}


