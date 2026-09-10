using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

public sealed class DeployOrchestrationVersionCommandHandler : IRequestHandler<DeployOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;
    private readonly IOrchestrationVersionArtifactSnapshotBuilder _artifactSnapshotBuilder;
    private readonly IOrchestrationArtifactPayloadFactory _artifactPayloadFactory;
    private readonly IArtifactPublicationApplicationService _artifactPublicationService;

    public DeployOrchestrationVersionCommandHandler(
        IOrchestrationVersionRepository repository,
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionTransitionPolicy transitionPolicy,
        IOrchestrationVersionArtifactSnapshotBuilder artifactSnapshotBuilder,
        IOrchestrationArtifactPayloadFactory artifactPayloadFactory,
        IArtifactPublicationApplicationService artifactPublicationService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
        _artifactSnapshotBuilder = artifactSnapshotBuilder ?? throw new ArgumentNullException(nameof(artifactSnapshotBuilder));
        _artifactPayloadFactory = artifactPayloadFactory ?? throw new ArgumentNullException(nameof(artifactPayloadFactory));
        _artifactPublicationService = artifactPublicationService ?? throw new ArgumentNullException(nameof(artifactPublicationService));
    }

    public async Task<bool> Handle(DeployOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.Deployed);

        var definition = await _definitionRepository.GetById(current.OrchestrationDefinitionId, cancellationToken);
        var versionSnapshot = await _artifactSnapshotBuilder.Build(current, cancellationToken);
        var artifactPayloadJson = _artifactPayloadFactory.CreatePayloadJson(definition, versionSnapshot);

        await _artifactPublicationService.PublishDeployment(new OrchestrationVersionDeployedEvent(
            current.Id.ToString(),
            current.OrchestrationDefinitionId.ToString(),
            definition.Name,
            current.VersionLabel,
            current.Version.ToString(),
            artifactPayloadJson,
            current.Checksum.Value,
            request.UpdatedBy,
            current.Id.ToString(),
            DateTime.UtcNow), cancellationToken);

        current.Status = OrchestrationVersionStatus.Deployed;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);

        return true;
    }
}
