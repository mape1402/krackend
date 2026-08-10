using Krackend.Sagas.Orchestrations.Contracts.Eventing;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.Design.Storage;
using Pelican.Mediator;

namespace Krackend.Sagas.Orchestrations.Design.Interaction;

/// <summary>
/// Handles deactivate orchestration definition command requests.
/// </summary>
public sealed class DeactivateOrchestrationDefinitionCommandHandler : IRequestHandler<DeactivateOrchestrationDefinitionCommand, bool>
{
    private readonly IOrchestrationDefinitionRepository _repository;
    private readonly IIntegrationEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeactivateOrchestrationDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="repository">Repository dependency.</param>
    public DeactivateOrchestrationDefinitionCommandHandler(
        IOrchestrationDefinitionRepository repository,
        IIntegrationEventPublisher eventPublisher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
    }

    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">Request to process.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True when the operation completes successfully.</returns>
    public async Task<bool> Handle(DeactivateOrchestrationDefinitionCommand request, CancellationToken cancellationToken)
    {
        var current = await _repository.GetById(PrimitiveParser.ParseId(request.Id), cancellationToken);
        await _repository.SetIsActive(PrimitiveParser.ParseId(request.Id), false, cancellationToken);
        await _eventPublisher.Publish(new OrchestrationDefinitionDeactivatedEvent(
            current.Id.ToString(),
            current.Key,
            current.Name,
            false,
            "web-ui",
            current.Id.ToString(),
            DateTime.UtcNow), cancellationToken);
        return true;
    }
}


