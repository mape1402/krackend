using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

public sealed class DeprecateOrchestrationVersionCommandHandler : IRequestHandler<DeprecateOrchestrationVersionCommand, bool>
{
    private readonly IOrchestrationVersionRepository _repository;
    private readonly IOrchestrationDefinitionRepository _definitionRepository;
    private readonly IOrchestrationVersionTransitionPolicy _transitionPolicy;
    private readonly IOrchestrationVersionArtifactSnapshotBuilder _artifactSnapshotBuilder;
    private readonly IIntegrationEventPublisher _eventPublisher;

    public DeprecateOrchestrationVersionCommandHandler(
        IOrchestrationVersionRepository repository,
        IOrchestrationDefinitionRepository definitionRepository,
        IOrchestrationVersionTransitionPolicy transitionPolicy,
        IOrchestrationVersionArtifactSnapshotBuilder artifactSnapshotBuilder,
        IIntegrationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _definitionRepository = definitionRepository ?? throw new ArgumentNullException(nameof(definitionRepository));
        _transitionPolicy = transitionPolicy ?? throw new ArgumentNullException(nameof(transitionPolicy));
        _artifactSnapshotBuilder = artifactSnapshotBuilder ?? throw new ArgumentNullException(nameof(artifactSnapshotBuilder));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    public async Task<bool> Handle(DeprecateOrchestrationVersionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.OrchestrationVersion current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);

        _transitionPolicy.EnsureCanTransition(current.Status, OrchestrationVersionStatus.Deprecated);

        current.Status = OrchestrationVersionStatus.Deprecated;
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        var definition = await _definitionRepository.GetById(current.OrchestrationDefinitionId, cancellationToken);
        var versionSnapshot = await _artifactSnapshotBuilder.Build(current, cancellationToken);
        var artifactPayloadJson = OrchestrationArtifactPayloadFactory.CreatePayloadJson(definition, versionSnapshot);

        await _eventPublisher.Publish(new OrchestrationVersionDeprecatedEvent(
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
