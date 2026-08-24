using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

public sealed class DeprecateOrchestrationVersionCommandHandler : IRequestHandler<DeprecateOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;
    private readonly IOrchestrationVersionArtifactSnapshotBuilder _artifactSnapshotBuilder;
    private readonly IArtifactPublicationApplicationService _artifactPublicationService;

    public DeprecateOrchestrationVersionCommandHandler(
        IOrchestrationVersionRepository repository,
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionTransitionPolicy transitionPolicy,
        IOrchestrationVersionArtifactSnapshotBuilder artifactSnapshotBuilder,
        IArtifactPublicationApplicationService artifactPublicationService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
        _artifactSnapshotBuilder = artifactSnapshotBuilder ?? throw new ArgumentNullException(nameof(artifactSnapshotBuilder));
        _artifactPublicationService = artifactPublicationService ?? throw new ArgumentNullException(nameof(artifactPublicationService));
    }

    public async Task<bool> Handle(DeprecateOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.Deprecated);

        current.Status = OrchestrationVersionStatus.Deprecated;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        var definition = await _definitionRepository.GetById(current.OrchestrationDefinitionId, cancellationToken);
        var versionSnapshot = await _artifactSnapshotBuilder.Build(current, cancellationToken);
        var artifactPayloadJson = OrchestrationArtifactPayloadFactory.CreatePayloadJson(definition, versionSnapshot);

        await _artifactPublicationService.PublishDeprecation(new OrchestrationVersionDeprecatedEvent(
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

        return true;
    }
}
