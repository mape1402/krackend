using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Distribution;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

public sealed class ArchiveOrchestrationVersionCommandHandler : IRequestHandler<ArchiveOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;
    private readonly IOrchestrationLifecycleArtifactPayloadFactory _lifecyclePayloadFactory;
    private readonly IArtifactPublicationApplicationService _artifactPublicationService;

    public ArchiveOrchestrationVersionCommandHandler(
        IOrchestrationVersionRepository repository,
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionTransitionPolicy transitionPolicy,
        IOrchestrationLifecycleArtifactPayloadFactory lifecyclePayloadFactory,
        IArtifactPublicationApplicationService artifactPublicationService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
        _lifecyclePayloadFactory = lifecyclePayloadFactory ?? throw new ArgumentNullException(nameof(lifecyclePayloadFactory));
        _artifactPublicationService = artifactPublicationService ?? throw new ArgumentNullException(nameof(artifactPublicationService));
    }

    public async Task<bool> Handle(ArchiveOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.Archived);

        current.Status = OrchestrationVersionStatus.Archived;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        var definition = await _definitionRepository.GetById(current.OrchestrationDefinitionId, cancellationToken);
        var lifecyclePayload = _lifecyclePayloadFactory.CreatePayload(
            definition,
            current,
            OrchestrationVersionReviewOperation.Archive);

        await _artifactPublicationService.PublishArchive(new OrchestrationVersionArchivedEvent(
            current.Id.ToString(),
            current.OrchestrationDefinitionId.ToString(),
            definition.Name,
            current.VersionLabel,
            current.Version.ToString(),
            lifecyclePayload.Json,
            lifecyclePayload.Checksum.Value,
            request.UpdatedBy,
            current.Id.ToString(),
            DateTime.UtcNow), cancellationToken);

        return true;
    }
}
