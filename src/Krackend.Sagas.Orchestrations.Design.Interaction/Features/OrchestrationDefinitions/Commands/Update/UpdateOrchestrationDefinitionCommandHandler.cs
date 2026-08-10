using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles update orchestration definition command requests.
/// </summary>
public sealed class UpdateOrchestrationDefinitionCommandHandler : IRequestHandler<UpdateOrchestrationDefinitionCommand, bool>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IDomainRepository _domainRepository;
    private readonly ITeamProjectionRepository _teamProjectionRepository;
    private readonly IIntegrationEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOrchestrationDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public UpdateOrchestrationDefinitionCommandHandler(
        IOrchestrationDefinitionRepository repository,
        IDomainRepository domainRepository,
        ITeamProjectionRepository teamProjectionRepository,
        IIntegrationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _domainRepository = domainRepository ?? throw new ArgumentNullException(nameof(domainRepository));
        _teamProjectionRepository = teamProjectionRepository ?? throw new ArgumentNullException(nameof(teamProjectionRepository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(UpdateOrchestrationDefinitionCommand request, CancellationToken cancellationToken)
    {
        global::Krackend.Sagas.Orchestrations.Design.Core.OrchestrationDefinition current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        var domain = await _domainRepository.GetById(PrimitiveParser.ParseId(request.DomainId), cancellationToken);
        var ownerTeam = await _teamProjectionRepository.GetById(PrimitiveParser.ParseId(request.OwnerTeamId), cancellationToken);
        if (!domain.IsActive)
        {
            throw new InvalidOperationException("Selected domain is disabled.");
        }
        if (ownerTeam is null || !ownerTeam.IsActive)
        {
            throw new InvalidOperationException("Selected team is invalid or disabled.");
        }

        current.Name = request.Name;
        current.DomainId = domain.Id;
        current.DomainDisplayName = domain.DisplayName;
        current.Domain = domain.DisplayName;
        current.OwnerTeamId = ownerTeam.Id;
        current.OwnerTeamDisplayName = ownerTeam.DisplayName;
        current.OwnerTeam = ownerTeam.DisplayName;
        current.Description = request.Description;
        current.Tags = request.Tags?.Distinct().ToList() ?? new List<string>();
        current.UpdatedOnUtc = DateTime.UtcNow;
        current.UpdatedBy = request.UpdatedBy;

        await _repository.Update(current, cancellationToken);
        await _eventPublisher.Publish(new OrchestrationDefinitionUpdatedEvent(
            current.Id.ToString(),
            current.Key,
            current.Name,
            current.IsActive,
            request.UpdatedBy,
            current.Id.ToString(),
            DateTime.UtcNow), cancellationToken);
        return true;
    }
}


