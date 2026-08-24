using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Enums;
using Krackend.Sagas.Orchestrations.ControlPlane.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;

/// <summary>
/// Creates distribution artifacts and promotion records from design lifecycle operations.
/// </summary>
public sealed class ArtifactPublicationApplicationService : IArtifactPublicationApplicationService
{
    private readonly IArtifactBuilder<OrchestrationVersionDeployedEvent> _deploymentBuilder;
    private readonly IArtifactBuilder<OrchestrationVersionDeprecatedEvent> _deprecationBuilder;
    private readonly IArtifactBuilder<OrchestrationVersionArchivedEvent> _archiveBuilder;
    private readonly IEnumerable<IArtifactValidationPolicy> _policies;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IReleaseRepository _releaseRepository;
    private readonly IReleaseTargetRepository _releaseTargetRepository;
    private readonly IRuntimeNodeRepository _runtimeNodeRepository;
    private readonly IOrchestrationNodePolicyRepository _policyRepository;
    private readonly IArtifactDeliveryApplicationService _deliveryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPublicationApplicationService"/> class.
    /// </summary>
    public ArtifactPublicationApplicationService(
        IArtifactBuilder<OrchestrationVersionDeployedEvent> deploymentBuilder,
        IArtifactBuilder<OrchestrationVersionDeprecatedEvent> deprecationBuilder,
        IArtifactBuilder<OrchestrationVersionArchivedEvent> archiveBuilder,
        IEnumerable<IArtifactValidationPolicy> policies,
        IArtifactRepository artifactRepository,
        IReleaseRepository releaseRepository,
        IReleaseTargetRepository releaseTargetRepository,
        IRuntimeNodeRepository runtimeNodeRepository,
        IOrchestrationNodePolicyRepository policyRepository,
        IArtifactDeliveryApplicationService deliveryService)
    {
        _deploymentBuilder = deploymentBuilder ?? throw new ArgumentNullException(nameof(deploymentBuilder));
        _deprecationBuilder = deprecationBuilder ?? throw new ArgumentNullException(nameof(deprecationBuilder));
        _archiveBuilder = archiveBuilder ?? throw new ArgumentNullException(nameof(archiveBuilder));
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _artifactRepository = artifactRepository ?? throw new ArgumentNullException(nameof(artifactRepository));
        _releaseRepository = releaseRepository ?? throw new ArgumentNullException(nameof(releaseRepository));
        _releaseTargetRepository = releaseTargetRepository ?? throw new ArgumentNullException(nameof(releaseTargetRepository));
        _runtimeNodeRepository = runtimeNodeRepository ?? throw new ArgumentNullException(nameof(runtimeNodeRepository));
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        _deliveryService = deliveryService ?? throw new ArgumentNullException(nameof(deliveryService));
    }

    /// <inheritdoc />
    public async Task PublishDeployment(OrchestrationVersionDeployedEvent deployment, CancellationToken cancellationToken = default)
    {
        var artifact = await GetOrCreateArtifact(_deploymentBuilder.Build(deployment), cancellationToken);
        await PromoteArtifact(artifact, deployment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishDeprecation(OrchestrationVersionDeprecatedEvent deprecation, CancellationToken cancellationToken = default)
    {
        await GetOrCreateArtifact(_deprecationBuilder.Build(deprecation), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishArchive(OrchestrationVersionArchivedEvent archive, CancellationToken cancellationToken = default)
    {
        await GetOrCreateArtifact(_archiveBuilder.Build(archive), cancellationToken);
    }

    private async Task<Artifact> GetOrCreateArtifact(Artifact candidate, CancellationToken cancellationToken)
    {
        Validate(candidate);

        var existing = await _artifactRepository.GetLatestForOrchestrationVersion(
            new Id(Ulid.Parse(candidate.OrchestrationVersionId)),
            candidate.ArtifactType,
            cancellationToken);

        if (existing is not null && string.Equals(existing.Checksum, candidate.Checksum, StringComparison.Ordinal))
        {
            return existing;
        }

        await _artifactRepository.Create(candidate, cancellationToken);
        return candidate;
    }

    private void Validate(Artifact artifact)
    {
        foreach (var policy in _policies.Where(x => x.CanValidate(artifact.ArtifactType)))
        {
            policy.Validate(artifact);
        }
    }

    private async Task PromoteArtifact(
        Artifact artifact,
        OrchestrationVersionDeployedEvent deployment,
        CancellationToken cancellationToken)
    {
        var allowedNodeIds = await _policyRepository.GetAllowedRuntimeNodeIds(
            artifact.OrchestrationDefinitionId,
            cancellationToken);

        if (allowedNodeIds.Count == 0)
        {
            return;
        }

        var enabledNodes = new List<RuntimeNode>();
        foreach (var nodeId in allowedNodeIds)
        {
            var node = await _runtimeNodeRepository.GetById(nodeId, cancellationToken);
            if (node.IsEnabled && node.Status == RuntimeNodeStatus.Active)
            {
                enabledNodes.Add(node);
            }
        }

        if (enabledNodes.Count == 0)
        {
            return;
        }

        var nodesNeedingTargets = new List<RuntimeNode>();
        foreach (var node in enabledNodes)
        {
            var existingTarget = await _releaseTargetRepository.GetByArtifactAndRuntimeNode(
                artifact.Id,
                node.Id,
                cancellationToken);

            if (existingTarget is null)
            {
                nodesNeedingTargets.Add(node);
                continue;
            }

            if (node.DistributionMode is DistributionMode.Push or DistributionMode.Hybrid
                && existingTarget.Status != ReleaseTargetStatus.Activated)
            {
                await _deliveryService.Push(existingTarget.Id.ToString(), "design-deploy", cancellationToken);
            }
        }

        if (nodesNeedingTargets.Count == 0)
        {
            return;
        }

        var release = new Release
        {
            Id = Id.New(),
            ArtifactId = artifact.Id,
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId,
            RequestedBy = string.IsNullOrWhiteSpace(deployment.Actor)
                ? "design-deploy"
                : deployment.Actor.Trim(),
            Strategy = "AutomaticDesignDeploy",
            Status = ReleaseStatus.InProgress,
            CreatedAtUtc = DateTime.UtcNow
        };

        var planTargets = nodesNeedingTargets.Select(node => new ReleasePlanTarget
        {
            Id = Id.New(),
            ReleaseId = release.Id,
            RuntimeNodeId = node.Id,
            Status = ReleaseStatus.InProgress,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = "Created automatically from Design deploy."
        }).ToArray();

        await _releaseRepository.Create(release, planTargets, cancellationToken);

        foreach (var node in nodesNeedingTargets)
        {
            var releaseTarget = new ReleaseTarget
            {
                Id = Id.New(),
                RuntimeNodeId = node.Id,
                ArtifactId = artifact.Id,
                ReleaseId = release.Id,
                RolloutGroup = release.Strategy,
                Status = GetInitialTargetStatus(node.DistributionMode),
                ActivationStatus = ActivationStatus.NotActivated,
                AssignedAtUtc = DateTime.UtcNow,
                AvailableAtUtc = node.DistributionMode == DistributionMode.Pull ? DateTime.UtcNow : null,
                CorrelationId = deployment.CorrelationId
            };

            await _releaseTargetRepository.Create(releaseTarget, cancellationToken);

            if (node.DistributionMode is DistributionMode.Push or DistributionMode.Hybrid)
            {
                await _deliveryService.Push(releaseTarget.Id.ToString(), release.RequestedBy, cancellationToken);
            }
        }
    }

    private static ReleaseTargetStatus GetInitialTargetStatus(DistributionMode mode)
        => mode == DistributionMode.Pull
            ? ReleaseTargetStatus.AvailableForPull
            : ReleaseTargetStatus.PushScheduled;
}
