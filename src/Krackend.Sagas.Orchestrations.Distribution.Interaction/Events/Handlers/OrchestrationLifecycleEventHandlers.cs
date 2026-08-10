using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Distribution.Core;
using Krackend.Sagas.Orchestrations.Distribution.Enums;
using Krackend.Sagas.Orchestrations.Distribution.Storage;

namespace Krackend.Sagas.Orchestrations.Distribution.Interaction;

public sealed class OrchestrationVersionDeployedEventHandler : IIntegrationEventHandler<OrchestrationVersionDeployedEvent>
{
    private readonly IArtifactBuilder<OrchestrationVersionDeployedEvent> _builder;
    private readonly IEnumerable<IArtifactValidationPolicy> _policies;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IReleaseRepository _releaseRepository;
    private readonly IReleaseTargetRepository _releaseTargetRepository;
    private readonly IRuntimeNodeRepository _runtimeNodeRepository;
    private readonly IOrchestrationNodePolicyRepository _policyRepository;
    private readonly IArtifactDeliveryInteractionService _deliveryService;

    public OrchestrationVersionDeployedEventHandler(
        IArtifactBuilder<OrchestrationVersionDeployedEvent> builder,
        IEnumerable<IArtifactValidationPolicy> policies,
        IArtifactRepository artifactRepository,
        IReleaseRepository releaseRepository,
        IReleaseTargetRepository releaseTargetRepository,
        IRuntimeNodeRepository runtimeNodeRepository,
        IOrchestrationNodePolicyRepository policyRepository,
        IArtifactDeliveryInteractionService deliveryService)
    {
        _builder = builder;
        _policies = policies;
        _artifactRepository = artifactRepository;
        _releaseRepository = releaseRepository;
        _releaseTargetRepository = releaseTargetRepository;
        _runtimeNodeRepository = runtimeNodeRepository;
        _policyRepository = policyRepository;
        _deliveryService = deliveryService;
    }

    public async Task Handle(OrchestrationVersionDeployedEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var artifact = _builder.Build(integrationEvent);
        foreach (var policy in _policies.Where(x => x.CanValidate(artifact.ArtifactType)))
        {
            policy.Validate(artifact);
        }

        await _artifactRepository.Create(artifact, cancellationToken);
        await PromoteArtifact(artifact, integrationEvent, cancellationToken);
    }

    private async Task PromoteArtifact(
        Artifact artifact,
        OrchestrationVersionDeployedEvent integrationEvent,
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

        var release = new Release
        {
            Id = Id.New(),
            ArtifactId = artifact.Id,
            OrchestrationDefinitionId = artifact.OrchestrationDefinitionId,
            RequestedBy = string.IsNullOrWhiteSpace(integrationEvent.Actor)
                ? "design-deploy"
                : integrationEvent.Actor.Trim(),
            Strategy = "AutomaticDesignDeploy",
            Status = ReleaseStatus.InProgress,
            CreatedAtUtc = DateTime.UtcNow
        };

        var planTargets = enabledNodes.Select(node => new ReleasePlanTarget
        {
            Id = Id.New(),
            ReleaseId = release.Id,
            RuntimeNodeId = node.Id,
            Status = ReleaseStatus.InProgress,
            CreatedAtUtc = DateTime.UtcNow,
            Notes = "Created automatically from Design deploy."
        }).ToArray();

        await _releaseRepository.Create(release, planTargets, cancellationToken);

        foreach (var node in enabledNodes)
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
                CorrelationId = integrationEvent.CorrelationId
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

public sealed class OrchestrationVersionDeprecatedEventHandler : IIntegrationEventHandler<OrchestrationVersionDeprecatedEvent>
{
    private readonly IArtifactBuilder<OrchestrationVersionDeprecatedEvent> _builder;
    private readonly IEnumerable<IArtifactValidationPolicy> _policies;
    private readonly IArtifactRepository _artifactRepository;

    public OrchestrationVersionDeprecatedEventHandler(
        IArtifactBuilder<OrchestrationVersionDeprecatedEvent> builder,
        IEnumerable<IArtifactValidationPolicy> policies,
        IArtifactRepository artifactRepository)
    {
        _builder = builder;
        _policies = policies;
        _artifactRepository = artifactRepository;
    }

    public async Task Handle(OrchestrationVersionDeprecatedEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var artifact = _builder.Build(integrationEvent);
        foreach (var policy in _policies.Where(x => x.CanValidate(artifact.ArtifactType)))
        {
            policy.Validate(artifact);
        }

        await _artifactRepository.Create(artifact, cancellationToken);
    }
}

public sealed class OrchestrationVersionArchivedEventHandler : IIntegrationEventHandler<OrchestrationVersionArchivedEvent>
{
    private readonly IArtifactBuilder<OrchestrationVersionArchivedEvent> _builder;
    private readonly IEnumerable<IArtifactValidationPolicy> _policies;
    private readonly IArtifactRepository _artifactRepository;

    public OrchestrationVersionArchivedEventHandler(
        IArtifactBuilder<OrchestrationVersionArchivedEvent> builder,
        IEnumerable<IArtifactValidationPolicy> policies,
        IArtifactRepository artifactRepository)
    {
        _builder = builder;
        _policies = policies;
        _artifactRepository = artifactRepository;
    }

    public async Task Handle(OrchestrationVersionArchivedEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        var artifact = _builder.Build(integrationEvent);
        var matchedPolicies = _policies.Where(x => x.CanValidate(artifact.ArtifactType)).ToArray();
        foreach (var policy in matchedPolicies)
        {
            policy.Validate(artifact);
        }

        await _artifactRepository.Create(artifact, cancellationToken);
    }
}
